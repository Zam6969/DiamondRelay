namespace VrcTwitchOscBridge.Services;

// Compatibility facade for saved profiles and UI wiring. This fork sends no heartbeats.
internal sealed class LiveFeedbackHeartbeatService : IDisposable
{
    public event Action<string>? DiagnosticLogged { add { } remove { } }

    public void UpdateState(bool enabled, bool hasBroadcaster, bool isLive,
        string displayName, string twitchLogin, string endpoint,
        string relayVersion, string buildChannel) { }

    public Task StopAsync() => Task.CompletedTask;
    public void Dispose() { }
}
