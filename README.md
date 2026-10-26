# DiamondRelay

An unofficial, modified Crystal Relay source distribution maintained in [Zam6969/DiamondRelay](https://github.com/Zam6969/DiamondRelay). Based on [seluvia/crystal-relay-public](https://github.com/seluvia/crystal-relay-public) at `aff72beeff5eab926bd7b8034e7281d87c566bb7`.

The app connects Twitch events to VRChat through OSC and OSCQuery. It includes avatar rules, movement triggers, supporter overrides, chatbox tools, themes, and localization. The program, updater, packages, and UI are branded DiamondRelay. Existing profiles are imported into the new local data folder without overwriting new settings. This is an independent distribution, not an official release of the original project.

## Changes in this distribution

- Removed the hard-coded developer chat dispatch path. No Twitch account receives implicit developer command privileges.
- Removed live feedback heartbeat transmission, including when old settings request it.
- Removed hosted World Guard checks and status requests. `!world` disclosure stays blocked locally, so world and creator identifiers are not uploaded to that service.
- Disabled in-app bug-report uploads. Review any report and submit a GitHub issue manually.
- Disabled hosted Ko-fi relay connections. Use Local Webhook mode with infrastructure you control. Existing hosted settings do not reconnect automatically.
- Removed supplemental developer About endpoints from loaded runtime configuration.
- Update checks and issue links now target this repository. No original-owner release packages are offered through its update feed.

See [SECURITY-REVIEW.md](SECURITY-REVIEW.md) for evidence, limitations, and remaining dependencies. These changes do not modify an existing installed copy of DiamondRelay; use a build from this source to obtain them.

## Build

Requires Windows and the .NET 10 SDK with Windows desktop support.

```powershell
dotnet build .\VrcTwitchOscBridge\VrcTwitchOscBridge.csproj -c Release
dotnet test .\VrcTwitchOscBridge.Tests\VrcTwitchOscBridge.Tests.csproj -c Release --filter "FullyQualifiedName~ForkSecurityTests|FullyQualifiedName~WorldCommandBlacklistServiceTests|FullyQualifiedName~DevChatCommandParserTests"
```

Run the resulting app from `VrcTwitchOscBridge/bin/Release/net10.0-windows/`. Review runtime settings before connecting accounts. Keep OSC and OSCQuery on local/private interfaces.

Twitch and VRChat authentication, optional StreamElements/Streamlabs integrations, and external emote/image services still require network access. The upstream Twitch OAuth client ID remains; it is a public application identifier, not a client secret. Login/session credentials continue to use Windows Credential Manager.

## License and attribution

The original MIT [LICENSE](LICENSE), including Copyright (c) 2026 ScreminPal, is preserved. The source includes the original OSCQuery library and its notices. Original license notices remain. This project is not endorsed by the upstream author.
