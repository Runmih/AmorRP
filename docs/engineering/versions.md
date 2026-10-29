# Versions and compatibility baseline

## Current product versions

Plugin and server **0.0.6.21**, Amor RP API **v2**, minimum client **0.0.6.18**.
The wire contract and database schema are unchanged by this maintenance release;
server 0.0.6.18 remains compatible with the plugin, but needs redeployment of the
new server source to receive the renewal-lock fix. XIVAuth's external API remains
**v1**. Dalamud API remains **15**. These are separate version boundaries.

The table below records the pinned toolchain. Release-specific behavior and
verification are in [0.0.6.21](../delivery/release-0.0.6.21.md). Historical milestone
records appear after the current compatibility/maintenance sections.

## Product release numbering

Use main only for development/publication. From the next feature release, use
`0.feature.0.patch`: first/third parts stay 0; completed feature releases increment
the second part and reset patch to 0; fixes increment the fourth part. The transition
from current 0.0.6.21 to next feature 0.1.0.0 is numerically increasing. Documentation
alone does not bump binaries. See the [release and hosting policy](../planning/hosting-and-releases.md).
These numbers do not change Amor RP API v2, XIVAuth API v1 or Dalamud API 15.

## Dependency matrix

| Component | Pinned baseline / boundary | Source of truth | Verification |
| --- | --- | --- | --- |
| Dalamud host | API 15; reference assembly 15.0.3.6 | `scripts/dalamud-reference.json` | Checksum-verified reference download and Windows build |
| Plugin SDK | Dalamud.NET.Sdk 15.0.0 | Plugin project and package lock | Locked restore and ZIP inspection |
| Plugin target | net10.0-windows, x64 | SDK-evaluated project | Windows packaging; manual game loading |
| .NET build SDK | 10.0.401 | `global.json`, Docker builder | Same SDK in local/CI builds |
| Server / Core / Contracts | net10.0; ASP.NET Core 10 for server | Shared project properties | Linux builds and contract boundary test |
| Server runtime | .NET / ASP.NET Core 10.0.12 | Pinned Docker runtime image | Container build/start checks |
| EF Core / migration tool | 10.0.12 | Central package pins and tool manifest | Locked restore, PostgreSQL tests and model consistency |
| Npgsql provider / driver | 10.0.3 | Central package pins and locks | PostgreSQL transaction/concurrency tests |
| PostgreSQL | Major 18; test image 18.6 | Compose/CI image digest; hosted deployment configuration | Real disposable test databases and migration tests |
| XIVAuth | Hosted OAuth; external character API v1 | Durable adapter, validated options and provider fixtures | Adapter tests plus separate live browser/renewal checks |
| Amor RP HTTP contract | OpenAPI 3.0.3; API v2 | `openapi.yaml`, endpoints and capabilities | Documentation/route checks and integration tests |
| Updates | HTTP polling; WebSockets planned | Plugin refresh/pollers and planned OpenAPI statuses | Refresh/coordinator tests; no implemented WSS guarantee |
| Tests | xunit.v3 3.2.2; runner 3.1.5; Microsoft.NET.Test.Sdk 18.10.1 | Central pins and test package locks | Linux server/contracts/helper workflow |
| Documentation | Python 3.12 recommended, minimum 3.10 | `scripts/requirements-docs.lock.txt` | Link, schema and endpoint metadata checks |
| CI | ubuntu-24.04 server; windows-2025 plugin | Commit-pinned workflows | Successful runs required for the published source |

SDK, host patch, runtime, product version and HTTP protocol version are independent.
Npgsql's EF provider 10.0.3 accepts EF Core >=10.0.4 and <11.0.0; the pinned EF Core
10.0.12 fits that range. Tests still need to verify the combination. Keep Dalamud's
host-provided libraries out of the plugin ZIP.

Render manages PostgreSQL minor updates: record the actual deployed version instead
of assuming it equals the test image. Production provider configuration and live
game behavior are separate evidence from automated fixtures. Primary references
and historical review dates are in [sources](../sources.md).

## Game integration compatibility

Use the SDK's Dalamud assembly references, including
`Dalamud.Bindings.ImGui`; do not independently upgrade or bundle replacements for
host-provided ImGui, Lumina or FFXIVClientStructs. The SDK package does not freeze
all DLLs supplied by an installed Dalamud host. CI must select an identified API
15 reference artifact and record its version/hash; release tests run against the
current stable host. Keep host-provided DLLs out of the distributable ZIP.

Prefer public services for character/context-menu access. API 15 removed
`IClientState.LocalPlayer` and `LocalContentId` in favor of `IPlayerState`, and
changed chat event arguments. Build the adapters against the API 15 services
instead of copying SDK 11 examples. Game object access belongs on the framework
thread; network completions must marshal needed game work to that thread. If
posting requires native client structures, isolate that dependency in the chat
adapter and repeat its live tests after patches.

Choose `IDalamudPlugin` as the initial lifecycle interface; keep network work
asynchronous and cancellable. API 15's newer asynchronous lifecycle interface
is an optional later choice after its load/dispose behavior is tested.

Use explicit `<AssemblyName>AmorRP</AssemblyName>` even when the project file is
`src/AmorRP.Plugin/AmorRP.Plugin.csproj`. Keep the manifest/internal name `AmorRP`,
DLL and config identity consistent. Folder names do not determine product identity.
Choose this once before distribution. Under API 15 the ZIP manifest must itself
be accurate; it is not corrected by replacing it with repository metadata at install.

Initial release support is Windows x64 with the stable global-client Dalamud
track. Other launcher/OS combinations and regional game clients require their
own live compatibility evidence before being listed as supported. Record actual
XIVLauncher version, OS build, Dalamud track, game build and plugin version in
manual test reports. A separate application runtime is controlled by XIVLauncher;
the plugin must not try to replace the host's bundled runtime.

## Product and persisted version boundaries

Keep these independent of package versions:

- Plugin and server build versions: record independently, with source commit and
  release notes; versioned alpha/beta artifacts precede product 1.0.
- HTTP and event protocol: negotiate through capabilities before mutations.
  Clients check the advertised minimum-client version before writes; raise that
  minimum when an update is required. A breaking wire change needs a new protocol,
  compatible deployment transition and explicit update guidance.
- Database migrations: version-controlled identifiers and tested schema range
  per server build. A newer schema does not justify blindly rolling back binaries.
- Item snapshots: `typeDataVersion` per type, initially 1; retain readers for
  existing revisions when adding newer representations.
- Local settings: explicit schema version, initially 1 for the replacement
  product. Test migration between new-product releases; legacy scaffold settings
  are not imported as authoritative identity, permissions or assets.
- Provider contract: XIVAuth API v1 in upstream source is evidence for a candidate
  adapter, not proof of hosted behavior or a guarantee of OpenID Connect support.
  Record verified URLs, scopes, attestation format/algorithms and renewal semantics
  at M1; isolate them from our API version and Core types.

Unknown types/versions show an update-required state and fail actions safely.
They must not be interpreted as a potion or letter based on a numeric fallback.

## Reproducible implementation and update policy

The following files record dependency pins; resolved pins are their source of truth:

- `global.json`: tested SDK baseline, explicit roll-forward policy, no previews.
  Recommend `latestPatch` within the selected feature band and record the resolved
  SDK in CI. Builder images use explicit patch tags and digests.
- `Directory.Packages.props`: central exact pins for compatible application/test
  projects. Verify interaction with SDK-generated plugin package references;
  if central management conflicts, opt the plugin out and pin its extra packages
  in its project. Do not override the SDK's own packager version to force alignment.
- `packages.lock.json` for each package graph and locked-mode CI restore.
- `.config/dotnet-tools.json`: local migration/tool versions; no reliance on
  whatever global `dotnet-ef` happens to be installed.
- `deploy/Dockerfile`, `deploy/compose.yaml`, `deploy/render.yaml`: matching runtime,
  database major, concrete images/digests and architecture. Avoid `latest` images.
- CI files: versioned Python/test tools and action SHAs. Documentation transitive
  dependencies are pinned in `scripts/requirements-docs.lock.txt`; hosted execution
  and release provenance remain to be demonstrated.

Review dependency/security updates monthly and before every release. Apply urgent
security fixes promptly with relevant regression checks. Minor/patch updates are
reviewed changes; automated proposals do not auto-deploy. Major updates need a
compatibility review and migration/test plan. Never freeze a security patch forever
because the initial planning document names it.

Game/Dalamud updates require checking load, character/home-world extraction,
context menu, selected chat channels and inventory UI. Backend updates require
authentication, transactions, retry/recovery and old supported client checks.
Database major updates require migration and restore drills. Refresh this dated
matrix when a baseline changes; exact deployed pins live in manifests and release
records rather than scattered constants throughout feature code.

## Historical milestone evidence

The following dated sections preserve earlier checkpoints. Version numbers, planned
features and then-pending gates below do not override the current versions above.

### Original milestone evidence

M0 records the resolved toolchain and dependency graph, creates the clean source
tree, proves clean CI build/tests and package integrity, and confirms the plugin
loads. M1 proves real provider and game integration using the recorded versions.
M4 exercises supported client/server combinations during deployment/reconnect.
M5 checks installation, new-product settings upgrade, schema compatibility and
backup restoration on the exact release artifacts. Test/image pins are now selected;
the M1 source-reviewed adapter is isolated from durable sessions. M0 game testing
and M1 were reported successful; M2 hosted renewal/game workflows and CI/container
evidence are recorded separately.

## M2 additions

Plugin `0.0.3.0`, server `0.0.3`, API v1, minimum product client `0.0.3`.
Host/SDK/.NET/PostgreSQL pins remain those above; M1 acceptance does not invent
new version evidence. `System.Security.Cryptography.ProtectedData` is pinned to
`10.0.12` for Windows CurrentUser DPAPI and included in the plugin package.
Server envelope encryption uses the .NET 10 AES-GCM implementation; OAuth/PKCE
uses .NET HTTP and WebUtilities against the same reviewed provider source.
Durable flow adds minimal `refresh` scope; hosted behavior is an M2 check.

## M3 additions

Plugin `0.0.4.0`, server/minimum client `0.0.4`, API v1, Dalamud API 15.

M3.1 patches: `0.0.4.1` adds permission diagnostics; plugin `0.0.4.2` also fixes
the potion header/button ImGui ID collision. Both use the existing M3 server `0.0.4`;
no new server version, API routes or database migration is required. See the
[patch guide](../delivery/m31-install-test.md).
SkiaSharp and SkiaSharp.NativeAssets.Linux.NoDependencies `4.153.1` are pinned
server-only image dependencies. Static PNG/JPEG/WebP decode/normalize passed on
Linux; matching upstream MIT and third-party notices ship with the server.
Current NuGet vulnerability scan reported no known vulnerable server packages.
Existing SDK, EF/Npgsql/PostgreSQL, Dalamud host and XIVAuth targets are unchanged.
See [M3 checks](../delivery/m3-install-test.md); game rendering and hosted container
behavior remain separate acceptance evidence.

## Initial M4 versions (historical; current revision below)

Plugin **0.0.5.0**, server/minimum client **0.0.5**, API **v1**, Dalamud API **15**.
The pinned official reference snapshot remains Dalamud **15.0.3.6**; this milestone
does not claim it is the newest upstream release. .NET SDK **10.0.401** / runtime
**10.0.12**, PostgreSQL **18.6**, and the existing locked package graph remain.
Only internal project version references change in lock files. No XIVAuth app,
callback URL, vault key or hosting-plan change is required.

Upgrade the server and database before testing plugin 0.0.5.0. M4 discovery rejects
an older/incompatible server. Existing M3 route aliases remain for compatible old
clients, but M4's new writing/trading interface requires the M4 server. Preserve the
vault key and DB connection; do not start over with a new identity or database.
See [M4 installation](../delivery/m4-install-test.md).

M4 plugin patch **0.0.5.1** fixes form movement during requests and distinguishes
in-flight actions from unresolved recovery. Server/minimum client remain 0.0.5;
no new migration. See the [focused window check](../delivery/m4-install-test.md).

## Current M4 trading revision — 0.0.5.2

Plugin, server and minimum client are **0.0.5.2**, API v1 and Dalamud API 15
remain unchanged. Both plugin and server must be updated. Apply the additive
DoubleTradeConfirmation migration before accepting traffic. Existing pending
first approvals are reset and revision advanced; no balances, offers, reservations,
identity bindings, quotas or history are erased. The second approval stage requires
the new `/final-confirm` endpoint. Older clients fail version discovery. See the
[M4 installation guide](../delivery/m4-install-test.md).

## M5 private usability checkpoint — 0.0.6.0

Plugin, server and minimum client are **0.0.6.0**. The testing series stays
0.0.6.x until maintainer approval and all release checks permit 1.0. API v1,
Dalamud API 15 / checksum-pinned 15.0.3.6 host references, .NET SDK 10.0.401,
runtime 10.0.12 and PostgreSQL 18.6 are unchanged. No new third-party package or
M4 schema migration is introduced. The plugin now bundles **AmorRP.Core.dll**
with Contracts for shared pure text rules. Core still has no dependency on
Contracts, transport, game host or persistence. Both server/plugin updates are
required; old clients fail minimum-version discovery. See the
[M5 private test guide](../delivery/m5-install-test.md).

Plugin **0.0.6.1** is a client-only cleanup patch compatible with server/minimum
client **0.0.6.0**, API v1 and the same schema. It removes M1 tools and chat
permission UI, makes login one action, and uses the maintainer-supplied HTTPS
release endpoint. Configuration version 4 drops the backend editor field without
retagging stored credentials or pending commands. No new runtime/package dependency.

The [custom testing feed](../delivery/plugin-repository.md) derives its version,
internal name and API level from the checked ZIP manifest. Published version bytes
are immutable; corrections and rollbacks require a higher plugin version. Feed
promotion does not change server/minimum-client versions or deploy the server.

Server **0.0.6.1** is a presentation-only callback update with a bundled background.
It retains minimum client **0.0.6.0**, API v1, the same database schema and existing
plugin **0.0.6.1** compatibility. No dependency/toolchain changes are introduced.

Plugin **0.0.6.2** simplifies the shared header and adds the supplied installer
icon. It remains compatible with server 0.0.6.0/0.0.6.1 and minimum client
0.0.6.0. The ZIP includes a validated square PNG; IconUrl identifies those bytes
by SHA-256. Dependencies, API, schema and saved settings do not change.

Plugin **0.0.6.3** adds independent Join/Create community windows and header entry
buttons. It retains API v1, the schema, saved settings and existing server
0.0.6.0/0.0.6.1 compatibility. No new dependency or migration.

Plugin **0.0.6.4** adds the two-icon launcher and badge-only incoming-trade
notifications across joined communities, using existing authenticated trade reads.
Configuration.ShowLauncher defaults true for existing users; version 4 and
protected credentials/commands stay compatible. API, schema, dependencies, server
0.0.6.0/0.0.6.1 compatibility and minimum-client requirements do not change.

Plugin **0.0.6.5** changes UI composition, transient feedback and automatic
context-menu roster selection only. API v1, schema, dependencies, configuration
version 4, server 0.0.6.0/0.0.6.1 compatibility and minimum client stay unchanged.

Security correction: plugin **0.0.6.6**, server **0.0.6.2**, minimum client
**0.0.6.6**, API v1 / Dalamud API 15. New login requires a browser-only pairing
code through LoginExchangeRequest; older code-free redemption is rejected.
New clients refuse older servers for fresh login. Existing saved-session renewal
remains compatible. Apply additive **20261015180055_LoginBrowserPairing** before
starting the fixed server; old in-flight handoffs are cancelled. Dependencies and
configuration version 4 are unchanged. All server instances must be upgraded.

Server **0.0.6.3** fixes review finding 2 by partitioning API/login limits per
client network and supporting explicit trusted-proxy processing. Plugin/minimum
client remain **0.0.6.6**. No new schema, endpoint, dependency or plugin package;
LoginBrowserPairing from server 0.0.6.2 remains required on older databases.
See operations for proxy trust and retained upstream abuse protection. Changes
can be batched with other review fixes before the next live deployment.

Server **0.0.6.4** completes finding 2 for Render's public web services: automatic
RENDER=true detection uses the edge-overwritten CF-Connecting-IP header instead
of needing static proxy IPs or a guessed hop count. Invalid or missing ingress
identity fails closed; local/direct hosting ignores this header. No schema,
endpoint, dependency or plugin version change; plugin/minimum remain **0.0.6.6**.

Server **0.0.6.5** fixes finding 3 with durable per-network login occupancy and
rolling-start limits. Migration **20261015191138_LoginNetworkAdmission** adds
origin fingerprints/start timestamps and cancels unattributed unfinished
handoffs, preserving saved sessions/assets. Shared ClientNetwork normalization,
purpose-derived HMACs and expiry cleanup support fair admission across restarts
and instances. No endpoint, dependency or plugin change; minimum/plugin **0.0.6.6**.

Server **0.0.6.6** fixes finding 4 by requiring active membership in a non-deleted
community for history feeds. Personal operation metadata recovery remains separate
and actor/participant-scoped. No schema, endpoint, dependency, Render setting or
plugin package change; plugin/minimum remain **0.0.6.6**. Previous migrations apply.

Server **0.0.6.7** fixes finding 5 by releasing the community write lock after expiry,
then building trade GET responses in a fresh read-only repeatable-read snapshot.
Page projection uses four set-based lookups, shared with locked command responses.
No schema, endpoint, dependency, configuration or plugin package change; minimum
and plugin remain **0.0.6.6**. Prior review migrations remain required.

Server **0.0.6.8** removes unsafe category name/type rewrites from the historical
TradingBeta migration. Existing categories are preserved on future upgrades;
new communities still seed Books. Already-applied databases do not rerun the migration
and require owner review/explicit repair where needed; a read-only report is
included. No new migration ID, schema, endpoint, dependency or plugin package;
plugin/minimum remain **0.0.6.6**. This does not claim production data was repaired.

## Security resource/audit correction — 0.0.6.9

Server **0.0.6.9**, plugin/minimum client **0.0.6.6**, API v1 and toolchain pins
unchanged. Adds migration **20261015201132_RetainedGroupCreationBudget** for immutable
original-creator accounting (30 retained communities by default). Readiness now uses one
background probe per process; both trade approval stages reject duplicate new-key
consent while preserving same-key receipt recovery. No plugin/feed promotion or
new dependency is needed. Existing data is preserved; see operations for backfill,
validated optional settings and readiness detection latency.

## Member profile UI — plugin 0.0.6.7 / server 0.0.6.10

Plugin **0.0.6.7** adds navigation icons and a simplified member profile with a
self-edited description and inline owner permissions. Server **0.0.6.10** adds
migration **20261015203517_MemberDescriptions**, the self-only description PATCH
and additive Member description response field. Minimum client stays **0.0.6.6**;
API v1, Dalamud API 15 and dependency/tool pins are unchanged. Deploy server before
using new profile edits. The custom repository URL/filename and plugin identity
are unchanged; no data wipe is included.

## Member controls — plugin 0.0.6.8 / server 0.0.6.11

Plugin **0.0.6.8** adds labeled currency/membership fields, automatic vertical
balance/Refresh, positive Trading, Craft Potions, moderator recipe grants and
Shift-enabled membership buttons. Server **0.0.6.11** adds optional atomic
MemberCapabilities.TradingAllowed and the independent items.potion.define grant.
No schema/migration or dependency changes. Minimum client remains **0.0.6.6**;
older capability bodies leave trading unchanged. New permission UI requires the
new server. Feed URL and plugin identity remain unchanged.

## Community Home candidate — plugin 0.0.6.9 / server 0.0.6.12

New migration **20261015232913_GroupAnnouncements**. Adds five announcement routes
and eight schemas, now 99 API operations / 132 schemas. General Moderation,
Manage Gameplay Permissions and Create Announcements are distinct opt-in actions.
Minimum client remains **0.0.6.6**; all new UI features require the matching server.
API v1, Dalamud API 15, dependency/tool pins and custom repository URL are unchanged.
No existing data or permission grants are reset. Read markers have server-persistent
idempotent metadata semantics without financial operation receipts.

## Inventory/text layout patch — plugin 0.0.6.10

Plugin-only: shared inventory layout/filter hierarchy, per-crafting quota display
and display-only multiline wrapping. Server remains **0.0.6.12**, minimum client
**0.0.6.6**, API v1 and all dependency pins unchanged. No migration or server
redeploy is required for this patch. Editor wrap/cursor/edit cases run in the
existing helper test project; actual Dalamud keyboard/layout acceptance is live.

## Trade request layout — plugin 0.0.6.11

Plugin-only Inbound/Outbound request tabs, active-exchange-only layout, explicit
community header and shared quick-launcher community selector. Server stays **0.0.6.12**,
minimum client **0.0.6.6**, API v1 and all dependency pins unchanged. No new server
deploy/migration. Pending invitations survive closing; accepted exchanges retain
close-to-cancel and authoritative revision/final-confirmation safeguards.

## Home overview/logos — plugin 0.0.6.12 / server 0.0.6.13

Matching server and CommunityLogos migration precede logo testing. API v1 and minimum
client 0.0.6.6 remain; optional Community logo fields and bootstrap limits are additive.
No toolchain/dependency changes. SkiaSharp normalizes bounded static images;
Dalamud's existing file dialog/texture services provide client selection/display.

## Footer and text limits — plugin 0.0.6.13 / server 0.0.6.14

Main, trade and community forms show action feedback in a footer with at most three
recent results. Each keeps its own diagnostic details, expires after 20 seconds,
and pauses while its details button is hovered/held or its popup is open. Reopening
clears ordinary feedback; unresolved commands retain receipt recovery controls.

Community, character and potion descriptions accept 300 Unicode text elements. All
Books contents (book, letter, note) accept 500. Typing and paste are bounded in
the plugin; API validation independently rejects larger writes. Existing stored
text remains readable and is not silently rewritten during upgrade. No migration.
ServicePolicy description/body overrides must be within 300/500 respectively.

Plugin **0.0.6.14** replaces the installer icon with the exact 512×512 red-and-gold
RP PNG supplied by the maintainer. The manifest points to a new content-hashed
image URL, and both the ZIP and feed publish those bytes together. Server remains
0.0.6.14; no additional server redeploy or migration is needed for this update.

## Clean main repository — plugin 0.0.6.15

The source and installer feed move to Runmih/AmorRP, branch main, with a new root
commit and no imported Git history. The public README explains the product, its
venue/community goal and the brief installation steps. The plugin description
contains only the product introduction and /amorrp → Main Menu. Repository,
license-source, support and icon URLs target this repository. Runtime features
and server version 0.0.6.14 are unchanged. No new migration or data wipe is needed.
