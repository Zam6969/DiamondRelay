using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VrcTwitchOscBridge.Models;
using VrcTwitchOscBridge.Services;
using Xunit;

namespace VrcTwitchOscBridge.Tests;

public sealed class ForkSecurityTests
{
    [Fact]
    public void LegacyProfilesCannotEnableHeartbeatOrDeveloperEndpoints()
    {
        var settings = new AppSettings { LiveFeedbackHeartbeatEnabled = true };
        Assert.False(settings.LiveFeedbackHeartbeatEnabled);
        var config = JsonSerializer.Deserialize<RuntimeConfig>(
            "{\"liveFeedbackHeartbeatEndpoint\":\"https://example.org/ping\",\"SupplementalAboutProfilesEndpoint\":\"https://example.org\"}")!.Normalize();
        Assert.Empty(config.LiveFeedbackHeartbeatEndpoint);
        Assert.Empty(config.SupplementalAboutProfilesEndpoint);
    }

    [Fact]
    public async Task UpdateCheckOnlyRequestsDiamondRelayReleases()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        using var service = new ApplicationUpdateService(client);
        await service.CheckForUpdateAsync(new ApplicationBuildIdentity(
            "3.2.0", ApplicationUpdateChannel.Stable, "", "3.2.0", 0, false), "", "", false);
        Assert.Equal("https://api.github.com/repos/Zam6969/DiamondRelay/releases?per_page=100", handler.RequestUri);
    }

    [Fact]
    public void IncomingChatHasNoDeveloperDispatchMethod()
    {
        Assert.Null(typeof(BridgeCoordinator).GetMethod("TryHandleDevChatCommandAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
    }

    [Fact]
    public async Task BugReportUploadIsDisabled()
    {
        using var service = new BugReportService();
        var result = await service.SubmitAsync(new BugReportSubmission(
            "title", "actual", "expected", "steps", "contact", "3.2.0", "other", "low", "snapshot", null, null, null));
        Assert.False(result.Succeeded);
        Assert.Contains("disabled", result.ErrorMessage);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri!.AbsoluteUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        }
    }
}
