# Decision register

**Agreed** means explicitly established in the conversation. **Recommended** is a
concrete baseline for implementation planning, subject to product review.
**Unverified** requires an integration experiment or external setup.

| ID | Status | Decision |
| --- | --- | --- |
| A01 | Agreed | FFXIV Dalamud interface for fictional RP items and currency. |
| A02 | Agreed | Closed groups isolate all items and currency; easy active-group switching. |
| A03 | Agreed | Up to three created groups and six joined groups; owned groups count toward joined. |
| A04 | Agreed | Initial item types are potions and letters. Other types wait until the foundation works. |
| A05 | Agreed | Group owner defines potions; authorized characters create copies. |
| A06 | Agreed | Individually authorized actions instead of named roles. |
| A07 | Agreed | Owner sets a group-wide weekly potion allowance; each authorized character has its own usage. Potion cost determines points per copy. |
| A08 | Agreed | Alt characters can have independent allowances. Alt management is the owner's responsibility. No account-level sharing or alt detection. |
| A09 | Agreed | Owner-configurable letter creation limits; five letters/week is the initial default. |
| A10 | Agreed | Weekly allowance reset. |
| A11 | Agreed | Potion use consumes one and posts its visible use message; initially 50 characters. Player selects Emote, Say, Party or Linkshell. The earlier startup-consent design is superseded by A30: no separate permission window or checkbox; explicit Use requests posting the previewed message. |
| A12 | Agreed | Context-menu trading; members can trade by default; owner can restrict a character. |
| A13 | Agreed | Evaluate XIVAuth for authentication and verified character identity. |
| A14 | Agreed | Existing project files may be completely replaced. The new product does not need to preserve the SDK 11 scaffold or its sample profile behavior. |
| A15 | Agreed | Character rename or home-world transfer must preserve inventory, currency, group memberships, permissions and quota usage. Ownership uses an internal character ID linked to provider-verified identifiers; names/worlds are mutable display/context fields. |
| A16 | Agreed | Skip live paid character rename/home-world-transfer tests because they require real-money services. No separate simulated-rename/transfer test gate is required. Verified ID/key mapping and authorization are covered by normal authentication tests; provider identifier derivation is documented. Tests must check meaningful behavior/security boundaries rather than hypothetical alternate implementations. |
| A17 | Agreed | The active-group selector is a dropdown outside/above the feature tabs, available from every tab. |
| A18 | Agreed | Currency symbols are owner-provided PNG, JPEG/JPG or WebP images, maximum 128x128 pixels, in the 1.0 scope. Text symbols in M2 are legacy compatibility data; general attachments remain deferred. |
| A19 | Agreed | Clear the join invitation code after confirmed join success; reset the group-creation form to defaults after confirmed creation. Keep drafts on failures or unresolved outcomes. |
| A20 | Agreed | Use CRUD for ordinary resources. Leaving and owner removal end the same membership with different actor permissions; preserve assets/usage and allow invited rejoining. Blocking/unblocking is separate. Current combined removal/blocking must be replaced. |
| A21 | Agreed | Written content category is Books, with book, letter and note subtypes. This is the intended classification, not evidence that all subtypes or book-specific behavior are already implemented. |
| A22 | Agreed | Literal potion messages must allow ordinary `<3` text, with clear feedback on invalid recipe messages. Both changes were deferred to M5 and are implemented for private testing in 0.0.6.0. |
| A23 | Agreed | M4 uses vertical main navigation with horizontal subtabs. Inventory is the default with its own subtabs. Home shows the group profile; crafting/creation has dedicated sections; History is tucked inside Settings. |
| A24 | Agreed | No Owner tab. Administrative actions appear in the relevant views and are hidden when unauthorized. The server remains authoritative; owners retain implicit permissions. |
| A25 | Agreed | Eliminate jarring full-view reloads on reads/actions. Background updates preserve selection, drafts, scroll and navigation, refresh relevant data quietly and retain local action feedback. Lightless is a visual reference. |
| A26 | Agreed | Trading opens in its own window: inventory left, own offer top-right, peer offer below; arrows add/remove chosen quantities. Currency saves on focus loss and ignores leading zeros. Replace Publish/Reload with both accepting then both approving a final popup. Any edit after acceptance starts cancels the whole trade. |
| A27 | Agreed | The maintainer reported that the 0.0.5.2 trade interaction feels better and approved progression to M5 on 2026-10-15. Final 1.0 release approval and production operating evidence remain separate. |
| A28 | Agreed | M5 first targets test candidates; support uses GitHub Issues. The maintainer subsequently requested an unlisted custom Dalamud testing feed on 2026-10-15 for easier updates: see [distribution](delivery/plugin-repository.md). Packages are publicly downloadable; official submission and 1.0 approval remain separate gates. No automatic issue/diagnostic posting is authorized. |
| A29 | Agreed | M5 private builds use version 0.0.6.x until testing/release checks pass and the maintainer approves 1.0. The first usability checkpoint is 0.0.6.0; this is not a completed M5 milestone. |
| A30 | Agreed | Plugin 0.0.6.1 removes the chat permission window/checkbox entirely and all player-facing M1 tools. Signed-out UI is one login button; XIVAuth opens and completes automatically. This supersedes earlier startup-consent instructions. |
| A31 | Agreed | The live release server is https://amorrp-server.onrender.com/ (supplied by the maintainer). Set it centrally and hide endpoint configuration from players. Preserve origin bindings on old sessions and pending commands. |
| R01 | Recommended | One repo; C# server, PostgreSQL, Render hosting. Plugin remains a separate deployment artifact. |
| R02 | Recommended | Apply three-owned/six-joined limits per verified character. The user's original wording did not settle account versus character for group caps. Confirm before finalizing group admission. |
| R03 | Recommended | Monday 00:00 UTC weekly boundary; no rollover; changes to weekly limits start next period. Potion allowance initially zero until owner configures it. |
| R04 | Recommended | Three grantable capabilities: `currency.manage`, `items.potion.create`, `inventory.remove`. Owner authority is implicit and cannot be delegated by a capability holder. |
| R05 | Recommended | One whole-unit currency per group. No per-week currency issuance quota in 1.0; record every authorized adjustment. |
| R06 | Recommended | Owner can enable/retire potion definitions. Initially all enabled potions are available to characters with creation permission. Per-recipe access waits beyond 1.0. |
| R07 | Recommended | Immutable potion revisions; existing copies retain description/message. Creation uses latest enabled revision and current cost. Cost edits invalidate stale creation requests. |
| R08 | Recommended | Letters editable only by author while held, permanently locked on first completed trade. Title 80 characters, body 5,000 maximum by default. |
| R09 | Recommended | Trade restriction blocks sending and receiving. One open trade per character per group; owner restrictions/removal cancel affected open trades. |
| R10 | Superseded by A20 | The original recommendation combined owner removal with blocking. Ordinary removal now permits rejoining; blocking is a separate decision. The current M3 runtime still implements the original combination. |
| R11 | Recommended | Ownership transfer requires recipient acceptance and checks group limits. Group deletion is soft deletion followed by a documented purge process. |
| R12 | Recommended | Invitation-only admission; no global player/plugin-user search, public group directory, or presence discovery. |
| R13 | Recommended | Closed beta through a custom plugin repository first. Official Dalamud submission is a separate review path. |
| R14 | Recommended | Provisional capacity target: 50 connected clients, five groups of ten testers, ten simultaneous trades, 10,000 holdings/group. Validate before public 1.0. |
| R15 | Recommended | Initial cleanup policy: login attempts 10 minutes, replayable events 24 hours, idempotent response replay 7 days, financial audit 365 days, deleted-group purge after 30 days. Confirm privacy notice and operator workflow before inviting testers. |
| R16 | Recommended / M0 build verified | Plugin baseline: stable Dalamud API 15, Dalamud.NET.Sdk 15.0.0, .NET 10. M0 compiles against checksum-pinned Dalamud 15.0.3.6 references; live game loading is pending. Recheck stable support at each release; API 16 is currently a development preview. |
| R17 | Recommended | Use the dated engineering version matrix: .NET 10 LTS, PostgreSQL 18 on Render, EF Core/Npgsql 10, SDK-provided game/UI assemblies; prove and pin exact dependencies at M0/M1. Initial live support is Windows x64 on the stable global-client Dalamud track. |
| R18 | Recommended | Global Refresh data button, last-update/stale indicator and 60-second read refresh while an authenticated product window is visible, preserving local drafts and pending action context. Include the baseline in M3 and verify load behavior before 1.0. |
| R19 | Recommended | Retain dormant/blocked memberships and assets internally; default to active-only roster for everyone, with owner-only Show inactive members for recovery. |
| R20 | Recommended | Deliver the reported M2 removal fix and persistent error diagnostics first within M3, without an M2.1 artifact; require a live plugin removal/access-loss/history check before M3 acceptance. Reconsider a patch only if data integrity or unauthorized access is found. |
| R21 | Recommended | Currency icons are static, decoded/validated and normalized to metadata-free PNG; recommended encoded upload ceiling 256 KiB, durable group-scoped DB storage and owner-only versioned/idempotent changes. No external image URLs or general attachment hosting. |
| U01 | Unverified | Register XIVAuth application; verify actual scopes/endpoints, character identifiers, provider renewal/revocation, and deployment callback. |
| U02 | Partially verified | Replacement scaffold builds and local M0 tests pass on R16/R17 baselines. Live plugin loading, hosted Windows CI, complete Docker build, context menu and chat sending remain gates. |
| U03 | Unverified | Render account, billing, region, domain, credentials, database restore, and measured capacity. No resources provisioned. |
| U04 | Unverified | Identify a human release/operator maintainer, testers, support route, and distribution route. |

The recommended choices provide a complete proposed behavior, rather than leaving
endpoint implementations to guess. Resolve R02, R03, R05, R08, R11, R13,
R14, and R15 at their milestone gates. They do not block documentation or the
initial identity/chat feasibility spikes.

## M2 implementation notes (2026-10-07)

M1 passed per maintainer report. M2 applies the recommended 3/6 group caps per
verified character, with independently allowed alts. Durable sessions are included
before product APIs: the temporary M1 probes cannot authorize groups. Scope expands
only to `character refresh`; a new exact callback and deployment encryption key
are needed. Hosted renewal remains a new M2 acceptance check. Category seeding is
in M3 alongside its first inventory migration. See the [M2 guide](delivery/m2-install-test.md).

M2 feedback and exact M3 sequencing are recorded in the [feedback plan](delivery/m2-feedback-m3-plan.md). The reported removal failure remains open; source inspection identifies a required-reason/hidden-error path, not a confirmed live response.

M3 implementation (2026-10-07): carried removal/UI fixes and 128x128 currency
media shipped with inventory alpha 0.0.4.0/0.0.4. Decoder is pinned SkiaSharp
4.153.1 (MIT with third-party notices), encoded uploads default 256 KiB and storage
is PostgreSQL. These are implementation choices within R18–R21; live acceptance
remains pending. See [M3 guide](delivery/m3-install-test.md).

M3 live review (2026-10-14): item removal, member removal, unblocking and rejoining
were reported working; potion production for owner/authorized member remains
unresolved. The interface requires structural redesign. See the
[feedback record and proposed M3.1/M4 split](delivery/m3-feedback-review.md).

M3 acceptance: after plugin 0.0.4.2 the maintainer confirms both participating
characters create/use potions with messages posted to game chat, and authorizes
progression to M4. This closes the production blocker, not the final-interface or
1.0 release gates. See the [M4 plan](delivery/m4-interface-plan.md). The vertical
navigation correction on 2026-10-15 supersedes the initially mistyped direction.
