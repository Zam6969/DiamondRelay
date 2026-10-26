using VrcTwitchOscBridge.Infrastructure;

namespace VrcTwitchOscBridge.Models;

public sealed class WorldCommandBlacklistSettings : ObservableObject
{
    public const string DefaultWorkerCheckEndpoint =
        "";

    public const string DefaultWorkerStatusEndpoint =
        "";

    /// <summary>
    /// The hosted guard is disabled in this fork. World disclosure remains blocked.
    /// This property always returns false.
    /// </summary>
    public bool IsEnabled
    {
        get => false;
        set { /* Hosted service removed. */ }
    }
}
