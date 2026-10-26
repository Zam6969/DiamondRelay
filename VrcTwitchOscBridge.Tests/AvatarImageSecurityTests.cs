using System.IO;
using System.Net;
using System.Net.Http;
using VrcTwitchOscBridge.Services;
using Xunit;

namespace VrcTwitchOscBridge.Tests;

public sealed class AvatarImageSecurityTests
{
    [Fact]
    public void ProductionHandlerDoesNotForwardCookiesOrRedirectAutomatically()
    {
        using var handler = AvatarImageService.CreateThumbnailHttpClientHandler();
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
    }

    [Theory]
    [InlineData("https://api.vrchat.cloud/api/1/file/test", true)]
    [InlineData("https://cdn.example.org/image.jpg", false)]
    [InlineData("https://api.vrchat.cloud.attacker.invalid/image.jpg", false)]
    [InlineData("https://api.vrchat.cloud:4443/image.jpg", false)]
    public async Task CookieOnlyGoesToExactVrChatApiOrigin(string url, bool expectedCookie)
    {
        using var fixture = new Fixture();
        await fixture.Service.GetAvatarImageAsync("avtr_test", null, url, default);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(expectedCookie ? "auth=synthetic-audit-cookie" : null, request.Cookie);
    }

    [Fact]
    public async Task CrossOriginRedirectNeverReceivesVrChatSession()
    {
        using var fixture = new Fixture();
        fixture.Handler.RedirectTo = new Uri("https://cdn.example.org/image.jpg");
        await fixture.Service.GetAvatarImageAsync("avtr_test", null, "https://api.vrchat.cloud/api/1/file/test", default);
        Assert.Equal(2, fixture.Handler.Requests.Count);
        Assert.Equal("auth=synthetic-audit-cookie", fixture.Handler.Requests[0].Cookie);
        Assert.Null(fixture.Handler.Requests[1].Cookie);
    }

    [Fact]
    public void SynchronousImageLoadUsesTheSameCookieBoundary()
    {
        using var fixture = new Fixture();
        fixture.Service.GetAvatarImage("avtr_test", null, "https://external.example.org/image.jpg");
        Assert.Null(Assert.Single(fixture.Handler.Requests).Cookie);
    }

    [Theory]
    [InlineData("http://127.0.0.1/image.jpg")]
    [InlineData("file:///example/image.jpg")]
    [InlineData("https://name:secret@api.vrchat.cloud/image.jpg")]
    public async Task InsecureOrCredentialBearingUrlsAreNotRequested(string url)
    {
        using var fixture = new Fixture();
        await fixture.Service.GetAvatarImageAsync("avtr_test", null, url, default);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Theory]
    [InlineData("../escaped")]
    [InlineData("..\\escaped")]
    [InlineData("avtr_test:stream")]
    [InlineData("/absolute")]
    public async Task InvalidAvatarIdsCannotWriteOrRequestThumbnails(string avatarId)
    {
        using var fixture = new Fixture();
        await fixture.Service.GetAvatarImageAsync(avatarId, null, "https://api.vrchat.cloud/image.jpg", default);
        Assert.Empty(fixture.Handler.Requests);
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task OversizedThumbnailIsNotCached()
    {
        using var fixture = new Fixture();
        fixture.Handler.Oversized = true;
        await fixture.Service.GetAvatarImageAsync("avtr_test", null, "https://api.vrchat.cloud/image.jpg", default);
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void ThemePathCannotEscapeThroughSimilarFolderPrefix()
    {
        Assert.Null(ThemeAssetStore.ResolveBackgroundImagePath(Path.Combine("ThemeAssetsOther", "outside.png")));
        Assert.Null(ThemeAssetStore.ResolveBackgroundImagePath(Path.Combine("..", "outside.png")));
        Assert.Equal(Path.Combine(AppDataPaths.ThemeAssetsFolder, "inside.png"),
            ThemeAssetStore.ResolveBackgroundImagePath(Path.Combine("ThemeAssets", "inside.png")));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "diamondrelay-image-test-" + Guid.NewGuid().ToString("N"));
        public RecordingHandler Handler { get; } = new();
        private readonly HttpClient client;
        public AvatarImageService Service { get; }
        public Fixture()
        {
            client = new HttpClient(Handler);
            Service = new AvatarImageService(client, Path.Combine(Root, "icons"), Path.Combine(Root, "cache"));
            Service.SetVrChatAuthCookie("synthetic-audit-cookie");
        }
        public void Dispose()
        {
            client.Dispose();
            Directory.Delete(Root, true);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(Uri Uri, string? Cookie)> Requests { get; } = [];
        public Uri? RedirectTo { get; set; }
        public bool Oversized { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var cookie = request.Headers.TryGetValues("Cookie", out var values) ? string.Join(";", values) : null;
            Requests.Add((request.RequestUri!, cookie));
            if (Requests.Count == 1 && RedirectTo is not null)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Redirect);
                redirect.Headers.Location = RedirectTo;
                return Task.FromResult(redirect);
            }
            return Task.FromResult(new HttpResponseMessage(Oversized ? HttpStatusCode.OK : HttpStatusCode.NotFound)
            {
                Content = new ByteArrayContent(Oversized ? new byte[8 * 1024 * 1024 + 1] : [])
            });
        }
    }
}
