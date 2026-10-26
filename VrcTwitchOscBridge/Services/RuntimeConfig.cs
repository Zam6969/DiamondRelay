using System.Text.Json.Serialization;

namespace VrcTwitchOscBridge.Services;

public sealed class RuntimeConfig
{
    public const string DefaultTwitchClientId = "gm0ihiq9yqljizmnbixp3l0os065l7";
    public const string DefaultLiveFeedbackHeartbeatEndpoint = "";

    [JsonIgnore]
    public string TwitchClientId => DefaultTwitchClientId;

    public string SupplementalAboutProfilesEndpoint { get; set; } = string.Empty;

    public string SupplementalAboutProfilesHeaderName { get; set; } = string.Empty;

    public string SupplementalAboutProfilesHeaderValue { get; set; } = string.Empty;

    [JsonPropertyName("liveFeedbackHeartbeatEndpoint")]
    public string LiveFeedbackHeartbeatEndpoint { get; set; } = DefaultLiveFeedbackHeartbeatEndpoint;

    public RuntimeConfig Normalize()
    {
        // Ignore legacy developer endpoints even when loading an existing installation.
        SupplementalAboutProfilesEndpoint = string.Empty;
        SupplementalAboutProfilesHeaderName = string.Empty;
        SupplementalAboutProfilesHeaderValue = string.Empty;
        LiveFeedbackHeartbeatEndpoint = string.Empty;
        return this;
    }

    public static RuntimeConfig CreateDefault() => new RuntimeConfig().Normalize();
}
