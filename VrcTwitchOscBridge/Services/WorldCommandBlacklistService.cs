using System.Net.Http;
using VrcTwitchOscBridge.Models;

namespace VrcTwitchOscBridge.Services;

internal enum WorldCommandBlacklistRefreshStatus { Disabled, Ready, RequestFailed, InvalidResponse }
internal sealed record WorldCommandBlacklistRefreshResult(
    WorldCommandBlacklistRefreshStatus Status, int WorldEntryCount = 0,
    int CreatorEntryCount = 0, DateTimeOffset? UpdatedAtUtc = null);
internal sealed record WorldCommandBlacklistDecision(bool IsBlocked, bool IsFailClosed)
{
    public static WorldCommandBlacklistDecision Allow { get; } = new(false, false);
    public static WorldCommandBlacklistDecision Block(bool isFailClosed = false) => new(true, isFailClosed);
}

// No hosted guard or location uploads. Keep !world disclosure blocked locally.
internal sealed class WorldCommandBlacklistService : IDisposable
{
    public WorldCommandBlacklistService() { }
    internal WorldCommandBlacklistService(HttpClient httpClient) { }
    public void Configure(WorldCommandBlacklistSettings settings) { }
    public void InvalidateCurrentSessionList() { }
    public Task<WorldCommandBlacklistDecision> EvaluateAsync(
        string worldId, string authorId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(WorldCommandBlacklistDecision.Block(isFailClosed: true));
    }
    public Task<WorldCommandBlacklistRefreshResult> RefreshAsync(
        WorldCommandBlacklistSettings settings, bool force, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new WorldCommandBlacklistRefreshResult(WorldCommandBlacklistRefreshStatus.Disabled));
    }
    public void Dispose() { }
}
