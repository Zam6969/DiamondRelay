using System.Net.Http;
using VrcTwitchOscBridge.Models;
using VrcTwitchOscBridge.Services;
using Xunit;

namespace VrcTwitchOscBridge.Tests;

public sealed class WorldCommandBlacklistServiceTests
{
    [Theory]
    [InlineData("wrld_11111111-2222-3333-4444-555555555555", "usr_aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")]
    [InlineData("", "")]
    public async Task DisclosureRemainsBlockedWithoutNetworkRequests(string worldId, string authorId)
    {
        using var client = new HttpClient(new RejectNetworkHandler());
        using var service = new WorldCommandBlacklistService(client);
        service.Configure(new WorldCommandBlacklistSettings { IsEnabled = true });
        var result = await service.EvaluateAsync(worldId, authorId);
        Assert.True(result.IsBlocked);
        Assert.True(result.IsFailClosed);
        var refresh = await service.RefreshAsync(new WorldCommandBlacklistSettings(), true);
        Assert.Equal(WorldCommandBlacklistRefreshStatus.Disabled, refresh.Status);
        Assert.False(new WorldCommandBlacklistSettings { IsEnabled = true }.IsEnabled);
    }

    [Fact]
    public async Task CallerCancellationIsRespected()
    {
        using var service = new WorldCommandBlacklistService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.EvaluateAsync("", "", cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RefreshAsync(new(), true, cts.Token));
    }

    private sealed class RejectNetworkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new Xunit.Sdk.XunitException("World identifiers must never leave the app.");
    }
}
