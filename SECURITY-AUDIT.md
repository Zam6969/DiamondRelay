# DiamondRelay repository security audit

Date: 2026-10-26. Baseline: `f950245e43fab6c4085e1e150d8a02b62d08202f` in `Zam6969/DiamondRelay`.

## Result

No additional active, hard-coded owner-access path was identified in the reviewed current source. Three security defects were found and fixed: VRChat cookie forwarding to untrusted thumbnail destinations, thumbnail cache path traversal, and a theme-directory containment error. These are confirmed source defects, not evidence of deliberate malicious intent.

The earlier removal of privileged `!screm` dispatch, developer heartbeat transmission, hosted World Guard calls, Ko-fi integration, and developer support/promotion links remains in place. This report does not claim that a finite review proves the absence of every backdoor or vulnerability.

## Coverage and methods

- Inventoried and pattern-scanned all **747 tracked files**, including 460 C# sources, 53 XAML files, seven PowerShell scripts, ten project files, workflows, HTML, Java sample files, media, and vendored example assets. This is automated whole-tree coverage plus focused manual review of security-sensitive flows, not a claim of manually verifying every line of every file.
- Reviewed network destinations and request construction, cookies/tokens, incoming Twitch commands, OSC discovery and receive boundaries, update download/extraction/application, process launches, Windows credential storage, settings/import paths, diagnostics, and build/CI scripts.
- Scanned **4,223 unique Git blobs across 858 commits** for high-confidence GitHub/Slack token formats, AWS access-key IDs, and private-key headers. No matches were found. This is a pattern scan, not exhaustive recovery or classification of all possible secrets. Historical behavior was not comprehensively audited.
- Restored and queried all **ten .NET projects** using NuGet's current vulnerability data, with transitive dependencies included. Every restore succeeded and no vulnerable packages were reported. This result does not cover arbitrary vendored binaries or Unity packages.
- Reproduced the two thumbnail flaws using an isolated loopback HTTP listener and a synthetic cookie. No real account credentials were used and no external account was contacted by the proof tests.
- Ran the updated application build and **428 targeted security/regression tests**, all passing. Sixteen new cases cover thumbnail credentials, redirects, URL schemes, avatar paths, response-size limits, and theme path containment. Also corrected a stale lowercase application filename in an existing updater test after the rename; production package validation was not weakened.
- Re-ran the repository's publication scan. The broad upstream suite is not represented as fully passing; earlier unrelated upstream failures are described in `SECURITY-REVIEW.md`.

## Confirmed findings and fixes

### A1 — High: VRChat cookie forwarded to arbitrary thumbnail URLs — Fixed

`Services/AvatarImageService.cs`, both `LoadVrChatThumbnail` and `LoadVrChatThumbnailAsync`, attached `Cookie: auth=...` whenever a session existed, without restricting the URL origin. Thumbnail URLs are persisted in avatar data and target/profile fields and are subsequently passed to the image loader. The loader also accepted HTTP and used automatic redirects.

**Precondition:** an attacker-controlled thumbnail URL must reach an image-loading path while VRChat authentication is active, for example through tampered/imported avatar/profile data. This review does not establish that an unauthenticated remote attacker can modify all such data through VRChat itself.

**Evidence:** the isolated original-code test received `auth=audit-only-synthetic-cookie` at a loopback thumbnail URL. The test used only a fabricated credential.

**Fix:** session cookies are attached only to the exact default-port HTTPS origin `api.vrchat.cloud`. Automatic redirects and ambient cookie storage are disabled. Each redirect is handled explicitly, capped, and revalidated; a CDN or other origin receives no VRChat cookie. HTTP, non-web schemes, and user-info URLs are rejected. Synchronous and asynchronous loads share this logic. Thumbnail responses are also limited to 8 MiB while streaming.

**Validation:** tests cover a legitimate API origin, an unrelated CDN, a lookalike hostname, a different port, cross-origin redirection, synchronous loading, invalid schemes, and an oversized response.

### A2 — Medium: Avatar IDs could escape the thumbnail cache — Fixed

The same service built thumbnail paths using `Path.Combine(cacheFolder, avatarId + ".jpg")`. It also used the avatar ID in custom-icon destination filenames without validation.

**Evidence:** the original-code proof loaded the ID `../escaped-audit` and wrote attacker-selected response bytes to `escaped-audit.jpg` outside the cache directory, before image decoding. The proof created and removed only temporary test files.

**Impact/precondition:** a crafted avatar ID and a fetchable thumbnail can overwrite writable files with a `.jpg` suffix outside the intended cache. This is not an established unrestricted executable-write or remote-code-execution primitive.

**Fix:** image and custom-icon writes require a nonempty avatar identifier of at most 128 ASCII letters, digits, underscores, or hyphens. Separators, rooted paths, traversal, and alternate-stream syntax are rejected before fetching or writing.

### A3 — Low: Theme asset containment accepted similarly prefixed sibling folders — Fixed

`ThemeAssetStore.ResolveBackgroundImagePath` in `Services/ThemeManager.cs` checked `StartsWith(themeAssetsRoot)` without a trailing directory separator. A path inside a sibling such as `ThemeAssetsOther` could pass. `ClearBackgroundImage` could then delete that resolved file.

**Impact/precondition:** crafted theme-path configuration could access/delete a file outside the intended assets directory, constrained to a similarly prefixed location. It was not arbitrary traversal to every filesystem path.

**Fix:** containment now compares against the normalized root plus its directory separator. Regression tests reject a similarly prefixed sibling and parent traversal, while accepting a real asset path. The tests do not delete actual user theme files.

## Backdoor and credential-path checks

- `TryHandleDevChatCommandAsync` is absent. `DevChatCommandParser.IsAuthorizedUser` rejects every account. Remaining private developer execution helpers have no production callers from incoming messages; references within their own unreachable helper chain and tests do not create a live command entry point. No runtime reflection/dynamic assembly-loading route to those methods was found.
- Twitch notifications use the expected secure Twitch WebSocket; reconnect URLs are restricted to the expected host. Ordinary command permissions use notification identity/badge data rather than the former developer-name exception.
- OSC UDP is bound to IPv4 loopback and rejects non-loopback senders. Discovery rejects non-loopback query and OSC targets. This is local-process trust, not per-process authentication: another process already running locally may still interact with OSC.
- Twitch and VRChat API credential requests use their service endpoints. The production VRChat API handler disables automatic redirects and implicit cookies. Session secrets use Windows Credential Manager; protected caches use per-user data protection.
- Update discovery targets `Zam6969/DiamondRelay`. Downloads require GitHub HTTPS, expected package names, a matching nonempty SHA-256 digest, and matching size when advertised. ZIP extraction checks destination containment and reparse points. Update application validates manifest/package structure. Release authors remain trusted software publishers; a compromised publisher can distribute malicious code despite a matching digest.
- No certificate-validation bypass, hidden startup persistence, arbitrary shell-command receiver, dynamic assembly downloader, or unexplained credential-posting endpoint was identified in the reviewed app/updater paths. Process launch code reviewed was associated with explicit links, restart/recovery, VRChat launching, or updates.

## Remaining lower-priority risks and review limits

1. **Resource limits elsewhere:** `CashPaymentProviderService.ReceiveWebSocketTextAsync` still accumulates provider frames without a total message-size cap. Chat emote images are downloaded before checking a byte-size ceiling. A malicious/compromised provider or image source could exhaust memory. No exploit against the live services was attempted. These paths do not establish owner access.
2. **Image egress policy:** non-API thumbnails can still make unauthenticated HTTPS requests to external origins. Credentials are restricted, but this is not a complete private-network request firewall for untrusted profiles.
3. **Supply-chain hardening:** CI actions use version tags, and a vendored Unity sample references a Git branch. Consider workflow dependency lockfiles or verified commit pinning and preserved package locks. See [GitHub's secure-use guidance](https://docs.github.com/en/actions/reference/security/secure-use). No compromised action was established.
4. **Legacy example runtimes:** vendored examples target older .NET versions. .NET 6 support ended on 2024-11-12; see [Microsoft's notice](https://devblogs.microsoft.com/dotnet/dotnet-6-end-of-support/). The desktop application itself targets .NET 10; referencing the compiled library does not mean it runs under a separate .NET 6 runtime.
5. **OAuth application ownership:** the upstream public Twitch client ID remains. A public client ID is not a secret or proof that its owner receives users' tokens, but independent application registration would remove the upstream account's control over that registration.
6. **Dependency scan enforcement:** the dependency-check script treats a successful package-list command as success; detecting an advisory in command output is not itself configured as a failing build gate. Audit results should be parsed/enforced in a future CI hardening change.
7. **Binary/media boundary:** seven prebuilt DLLs are present in the vendored Unity examples. The production application project references the library source rather than those example binaries. This audit did not reverse engineer, antivirus-certify, or independently reproduce those DLLs, fonts, images, or media. NuGet advisories do not verify every such artifact.
8. **Scope boundary:** no live Twitch/VRChat account penetration test, hosted-server audit, installed-binary comparison, repository account-permission review, or full GitHub Actions supply-chain audit was performed. Older commits retain the original removed behavior; use the cleaned latest branch rather than old commits or upstream binaries.

9. **Unusual local behavior:** the app still has optional local easter eggs. A local Konami-key sequence plays the bundled bean-advertisement.mp4 and consumes app key events during playback. This is gated by EasterEggsEnabled; no incoming-chat or remote-owner trigger was identified. It is documented here because hidden media behavior can be surprising.

## Operational conclusion

The reviewed current source has no additional identified active developer backdoor, and the three confirmed defects above are remediated by this audit's patch. That is an evidence-based review result, not an absolute security guarantee. Existing installed or downloaded executables do not acquire these fixes until rebuilt or replaced with a build of the corrected source.
