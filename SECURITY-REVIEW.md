# Source review and remediation

Reviewed 2026-10-26. Upstream baseline: `seluvia/crystal-relay-public@aff72beeff5eab926bd7b8034e7281d87c566bb7`. Findings describe observable source behavior, not the author's intent.

## Confirmed concerns

1. **Hard-coded remote chat privilege.** `Services/DevChatCommandParser.cs` authorized Twitch login OR display name matching `Screminpal_`. `BridgeCoordinator.TryHandleDevChatCommandAsync` accepted `!screm grow`, `shrink`, `scalerandom`, `scaleset`, `move`, `moverandom`, and `firesale`. This branch preceded ordinary rule handling and did not ask the streamer to configure that privilege. The runtime dispatch method and its call are removed; authorization now rejects every account. Unreachable private execution helpers and parser types remain for compatibility with existing tests; they are not connected to incoming chat.

2. **Heartbeat could not be disabled.** `Models/AppSettings.LiveFeedbackHeartbeatEnabled` returned true, accepted only true assignments, and `SettingsStore` forced true when loading. This contradicted the upstream README's claim that it could be disabled. `LiveFeedbackHeartbeatService` posted live channel identity, URL, version, and build state to the developer's worker. This fork replaces the service with a no-network compatibility facade and ignores legacy enable flags and endpoints.

3. **Mandatory world metadata checks.** `WorldCommandBlacklistService.EvaluateAsync` sent world and creator IDs to `crystal-relay-world-guard.screminpal-animation.workers.dev`. The corresponding setting always returned true. The server could block `!world` disclosure; an outage also blocked it. The fork sends no guard requests and keeps world disclosure blocked locally, preserving privacy instead of allowing previously protected links.

4. **Original-owner software update trust.** `ApplicationUpdateService` queried the original repository's releases. Installing a future upstream package could replace modified code. The release API now targets `Zam6969/DiamondRelay`; the security tests assert the actual request URI.

5. **Developer-hosted Ko-fi relay.** The desktop authenticated to an upstream-hosted WebSocket and sent its relay client secret and Ko-fi verification token. Trust in that server was required; its storage/privacy promises cannot be established from the desktop source. Ko-fi integration is now removed entirely: no hosted connection, local webhook listener, configuration UI, credential reads/writes, or provider selection. Imported rules for its legacy provider ID remain disabled. Upstream donation/Discord commands and promotional About content are also removed.

6. **Bug report upload.** `BugReportService` assembled its worker URL from string fragments. This is unusual but does not establish malicious intent. Submission was user initiated and could include diagnostic data. Uploads are disabled in this distribution; preview/formatting remain available, and manual issue links target this repository.

7. **Supplemental About endpoint.** Optional runtime configuration could direct About data requests to an external service. Normalization now clears this endpoint and its custom header fields, including values from existing profiles.

## Other observations and limits

- The public-safety workflow blocks several ordinary AI/tool-related words in tracked text. This is a publication policy, not a runtime security control or evidence that the app is safe. It remains upstream behavior; this report is not based on that scan.
- Desktop input locking, VRChat account access, local avatar cache reads, and Twitch reward management are powerful normal features. Their presence is not proof of credential theft.
- The upstream OAuth client ID remains public and does not itself give the upstream author access to your tokens. A separately registered Twitch application would establish independent application ownership.
- No credential-stealing or arbitrary remote shell path was established in the reviewed source. This is a focused source review, not a guarantee that all vulnerabilities have been found, a dependency audit, server audit, or verification of previously distributed binaries.
- Original release binaries were not run. Existing installed copies and historical upstream commits retain their original behavior. Build the cleaned main branch; do not install upstream releases expecting these changes.

## Validation

The Windows Release application compiles with .NET 10. Security regression tests cover rejected developer authorization, removed command dispatch, disabled telemetry despite legacy settings, the fork update request, disabled report upload, cancellation handling, and world disclosure without network requests. Hosted guard protocol tests were replaced with tests of the new local-only behavior.

Validation: all 278 targeted security, update, developer-command, and world-privacy tests passed. A broader upstream suite run was stopped after failures and slow/hanging tests. A separate unchanged-baseline run reproduced five failures out of ten tests across payment summaries, scale timer fairness, and movement window layout. Application behavior has not been tested against live Twitch or VRChat accounts.

Ko-fi removal validation: legacy connection settings are ignored, imported provider-ID-2 rules cannot be enabled, and webhook/relay entry points and promotional commands are absent. The expanded targeted suite passes 278 tests.

## Expanded repository audit

See [SECURITY-AUDIT.md](SECURITY-AUDIT.md) for the whole-tree scan, historical secret-pattern scan, dependency results, three additional confirmed fixes, and 428 passing targeted tests.
