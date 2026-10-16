# Repository layout and navigation

## Current versus target

M0 replaced the sample/SDK 11 source, goat asset, obsolete relay/backend scaffolds
and IDE artifacts with four application projects under `src/`. Contracts currently
has `Common/` DTOs; Core has `Groups/` capacity policy. Server has `Health/` and
`Capabilities/` endpoints, persistence/migrations and options. Plugin has
`Diagnostics/` and pure HTTP helpers, with complete lifecycle unsubscription.
Three test projects exist: Contracts, Plugin and Server.IntegrationTests.

M1 adds `Contracts/Feasibility/`, `Server/Features/Feasibility/` (provider options,
adapter, bounded temporary store and six probe routes), `Plugin/Services/Game/`
(context-menu capture and chat submission), and (historically) `Diagnostics/FeasibilityPanel`. The latter and the chat
permission window are removed in plugin 0.0.6.1; the current shell is
`Plugin/Features/Shell/MainWindow.cs`.
These spike sessions are deliberately separate from future durable authentication
and persistence. See [M1 setup](../delivery/m1-install-test.md).

M2 adds implemented feature folders:

```text
src/AmorRP.Contracts/
  Auth/ Groups/ Currency/ History/ Common/      # wire records, no EF/game dependencies
src/AmorRP.Server/
  Features/Authentication/                      # persistent login/session/provider adapter
  Features/Groups/                             # access + GroupService, membership/invite/owner partials
  Features/Currency/                           # balance queries and transactional adjustment
  Features/History/                            # personal/owner logs and own receipt recovery
  Infrastructure/Persistence/Entities/         # one file per stored entity
  Infrastructure/Persistence/CommandStore.cs   # DB locks, idempotency, version preconditions
  Infrastructure/Persistence/PageCursor.cs     # actor/list-bound opaque pagination
  Infrastructure/Security/                    # safe faults and versioned secret vault
  Options/SessionPolicyOptions.cs             # validated lifecycle limits
src/AmorRP.Plugin/
  Features/Groups/GroupsPanel.cs                # async login/state orchestration
  Features/Groups/GroupScreens.cs               # group/profile/history screens
  Features/Groups/MemberScreens.cs              # roster and contextual member actions
  Services/Api/FoundationClient.cs              # product HTTP, same-key retries, safe errors
  Services/Identity/SavedSession.cs             # DPAPI credentials and unresolved command save
```

The provider's durable adapter remains next to its authentication feature in M2;
M1's adapter stays in Feasibility. M3 implements inventory/catalog/media folders below.
GroupService's partial files keep the shared transaction/access boundary while
separating memberships, invitations and ownership for navigation. Persistence
entities never leave Server; transport DTOs stay in Contracts. The plugin also
uses pure Core text rules in the M5 checkpoint.

Dependency notices are in `licenses/` and travel in the plugin ZIP.

The solution, dependency/tool pins, package locks, deployment recipes, source/package
scripts and two CI workflows exist. Required JSON is no longer blanket-ignored.
Git history, current documentation and license are preserved. No extra backend
implementation or empty future feature folders remain. See [M0 setup](../delivery/m0-install-test.md).

## Target layout

```text
Amor-RP/
├── README.md
├── LICENSE.md
├── CONTRIBUTING.md                    # implemented; verified setup guide
├── SECURITY.md                        # reporting route at release preparation
├── AmorRP.sln
├── global.json                        # verified/pinned toolchain, tracked explicitly
├── Directory.Build.props              # common warnings/style; platform-specific overrides
├── Directory.Packages.props           # exact application/test package pins; SDK-aware scope
├── .config/dotnet-tools.json           # local migration/tool versions
├── .editorconfig
├── .gitignore                         # targeted build/cache/secrets rules
├── .github/workflows/
│   ├── server-ci.yml
│   ├── plugin-ci.yml
│   └── release.yml
├── src/
│   ├── AmorRP.Contracts/
│   │   ├── Auth/ Groups/ Inventory/ Currency/ Trades/ Events/
│   │   └── Common/                    # IDs, pagination, ProblemDetails
│   ├── AmorRP.Core/
│   │   ├── Groups/                    # policies and membership use cases
│   │   ├── Inventory/
│   │   │   ├── Types/Potions/
│   │   │   ├── Types/Letters/
│   │   │   └── Quotas/
│   │   ├── Currency/
│   │   ├── Trades/
│   │   └── Common/                    # clock and transaction interfaces
│   ├── AmorRP.Server/
│   │   ├── Program.cs                 # composition only
│   │   ├── Features/
│   │   │   ├── Auth/ Groups/ Categories/ Definitions/
│   │   │   ├── Inventory/ Currency/ Trades/ History/
│   │   │   └── Account/ Events/ Health/ Capabilities/
│   │   ├── Infrastructure/
│   │   │   ├── Persistence/           # DbContext, mappings, migrations, transactions
│   │   │   ├── Identity/XIVAuth/      # provider adapter; no provider DTO leakage
│   │   │   ├── Sessions/ Idempotency/ Notifications/
│   │   │   └── Observability/
│   │   ├── Options/                   # typed options and startup validation
│   │   └── appsettings.json           # nonsecret defaults only
│   └── AmorRP.Plugin/
│       ├── Plugin.cs                  # services/lifecycle registration only
│       ├── Features/
│       │   ├── Auth/ Groups/ Inventory/ Consumables/ Letters/
│       │   ├── Creation/ Trading/ Administration/ History/
│       │   └── Settings/ Diagnostics/
│       ├── Services/
│       │   ├── Api/                   # HTTP client, outcome recovery, event subscription
│       │   ├── Game/                  # character/context-menu/chat adapters
│       │   └── LocalStorage/          # versioned settings, drafts, secure credentials
│       ├── UI/                        # shared widgets/type renderers
│       └── Assets/                    # curated icons; replace template goat
├── tests/
│   ├── AmorRP.Core.Tests/
│   ├── AmorRP.Server.IntegrationTests/ # real disposable PostgreSQL
│   ├── AmorRP.Contracts.Tests/
│   └── AmorRP.Plugin.Tests/           # pure presenters/adapters, not ImGui clones
├── deploy/
│   ├── Dockerfile                     # repo-root context for Core + Contracts + Server
│   ├── start-server.sh                # opt-in startup migrations for free single-instance testing
│   ├── render.yaml                    # secrets by reference only
│   ├── compose.yaml                   # local server + PostgreSQL
│   └── .env.example                   # names and placeholders only
├── scripts/
│   ├── check-docs.py                  # link/contract checks, implemented now
│   ├── check-server-startup.py        # migration failure and container process handling
│   ├── requirements-docs.txt           # pinned documentation-check dependencies
│   └── export-openapi.*               # contract export/compare in implementation
└── docs/
    ├── README.md
    ├── decisions.md
    ├── feasibility.md
    ├── sources.md
    ├── product/                       # scope, rules, interface, expansion
    ├── engineering/                   # layout, architecture, API, auth, security, data
    └── delivery/                      # development, roadmap, acceptance, operations
```

This tree describes the complete target, including future features. M0's projects,
deployment/CI files and contributor guide exist; later feature folders,
Core.Tests, SECURITY.md and release publication workflow are still planned.
Use these four application projects initially; split another assembly only when
it enforces a useful dependency boundary. Avoid empty future-feature folders.
Track per-project package locks. Keep the plugin assembly/internal name explicitly
`AmorRP` despite its `AmorRP.Plugin` project filename; see [versions](versions.md)
for framework, SDK, host-library and manifest requirements.
Within a feature use concrete names such as `CreatePotionHandler.cs`,
`CreatePotionEndpoint.cs`, `PotionDefinition.cs`, `PotionDetailsView.cs`; avoid
miscellaneous `Helpers`, `Managers`, or oversized all-feature services.

## Finding and changing a feature

| Need | Location in target tree |
| --- | --- |
| Change a player rule | `Core/<feature>/` plus product rules and acceptance scenario |
| Change HTTP shape | `Contracts/<feature>/`, endpoint, OpenAPI |
| Fix database mapping or migration | `Server/Infrastructure/Persistence/` |
| Fix XIVAuth | `Server/Infrastructure/Identity/XIVAuth/`, auth feature |
| Fix an inventory window | `Plugin/Features/Inventory/` or type renderer |
| Fix game chat or context menu | `Plugin/Services/Game/` |
| Add a type | Core type handler, contract schema, renderer, migration, type-specific tests |
| Change hosting | `deploy/`, operations runbook; no domain changes |

Do not share server domain entities with the plugin. Keep code adjacent to its
feature rather than creating a global folder for every syntactic class category.

## Replacement hygiene

M0 replaced `*.json` ignore with explicit ignores for `bin/`, `obj/`, `.vs/`,
`node_modules/`, generated output, local secret files, and developer overrides.
Track SDK pins, package manifests/locks, plugin manifests, and nonsecret config.
Previously ignored configuration was reviewed/replaced with nonsecret defaults;
future local overrides and credentials must remain ignored.
Separate plugin manifest generation from runtime configuration. Recreate the
solution and metadata with real product values. New lifecycle code must unregister
commands/UI/context subscriptions on disposal; no sample behavior needs preserving.

## M3 implemented feature map

| Change | Source location |
| --- | --- |
| Inventory wire data | `Contracts/Inventory/`, one public record per file |
| Safe plain/chat text | `Core/Items/ItemText.cs` |
| Categories and immutable potion revisions | `Server/Features/Catalog/` |
| Holdings, production, usage, consume/discard/removal | `Server/Features/Inventory/InventoryService.cs` |
| Private letter read/edit rules | `Server/Features/Inventory/InventoryService.Letters.cs` |
| Currency upload/normalize/authenticated read | `Server/Features/Media/` |
| Composite keys/constraints and migration | `Server/Infrastructure/Persistence/InventoryModel.cs`, entity files and PlayableInventory migration |
| Shared async product state | `Plugin/Features/Groups/GroupsPanel.cs` |
| Group forms/removal/icon editor | `Plugin/Features/Groups/GroupScreens.cs`, `GroupInteractionState.cs` |
| Inventory, potion, letter and catalog screens | Named `*Screens.cs` files in `Plugin/Features/Groups/`; they share the selected verified group state |
| Separate small window | `Plugin/Features/Inventory/CompactWindow.cs` |
| Bounded authorized texture lifecycle | `Plugin/Services/Media/GroupImageCache.cs` |
| Real DB accounting/privacy/upgrade checks | `tests/AmorRP.Server.IntegrationTests/InventoryTests.cs`, `InventoryUpgradeTests.cs`; same partial foundation fixture |

Shared UI partials stay beside group state so they cannot silently select another
character/group; type-specific potion and letter files remain easy to locate.
M4 trading is implemented in the map below. Future item types require their own
behavior/contracts/renderer and deliberate schema migration, preserving stable IDs.

## M4 implemented feature map

| Change | Exact source location |
| --- | --- |
| Trading requests/responses | `src/AmorRP.Contracts/Trading/`, one record per named file |
| Trade HTTP operations | `src/AmorRP.Server/Features/Trading/TradeEndpoints.cs` |
| Trade auth/transitions/recovery queries | `TradeService.cs` in that feature |
| Offer reservations and atomic net exchange | `TradeService.Offers.cs` |
| Shared cancellation/expiry release | `TradeReservations.cs`, `TradeExpiryWorker.cs` |
| DB entities/constraints | `Infrastructure/Persistence/Entities/TradeRow.cs`, `TradeSlotRow.cs`, `TradeLineRow.cs`, `TradingModel.cs` |
| Unified membership deletion / separate block | `Features/Groups/GroupService.Memberships.cs`, `GroupEndpoints.cs` |
| Canonical quantity reduction/deletion | `Features/Inventory/InventoryService.cs`, `InventoryEndpoints.cs`; `Contracts/Inventory/HoldingChange.cs` |
| Books subtype compatibility | Existing `Letter` contract/entity/service files plus TypeId; `CatalogService.Seed` |
| Vertical main / horizontal subtab navigation | `src/AmorRP.Plugin/Features/Groups/NavigationScreens.cs` |
| Quiet affected-data reads | `QuietUpdates.cs`, GroupsPanel's separate foreground/background lanes |
| Trade commands / polling / conflict handling | `TradeScreens.cs` |
| Separate trade layout / final popup | `TradeWindowScreens.cs`, `src/AmorRP.Plugin/Features/Trading/TradeWindow.cs` |
| Local draft preservation / wire currency normalization | `src/AmorRP.Plugin/Services/Api/TradeDraft.cs`, `src/AmorRP.Contracts/Currency/WholeUnits.cs` |
| Additive two-stage upgrade | `Infrastructure/Persistence/Migrations/20261015015854_DoubleTradeConfirmation.cs` |
| Context-menu adapter | `src/AmorRP.Plugin/Services/Game/CharacterContext.cs` |
| Meaningful real DB trade checks | `tests/AmorRP.Server.IntegrationTests/TradingTests.cs`, `TradingBoundaryTests.cs`, `DoubleConfirmationTests.cs`, `InventoryUpgradeTests.cs` |
| Upgrade and live testing | `docs/delivery/m4-install-test.md` |

Legacy filenames/fields containing Letter intentionally preserve existing wire/data
compatibility while TypeId distinguishes book/letter/note. Group UI partials share
one selected context; there is no separate hidden owner application. Do not create
empty future folders or put credentials, DB contents or build artifacts in Git.

`Plugin/Features/Groups/StatusScreens.cs` owns the group-only header, optional
feedback footer, Settings connection tools and receipt controls. Plugin 0.0.6.2
removes the earlier multirow header and empty idle feedback.

## M5 usability source map

| Responsibility | Source |
| --- | --- |
| Shared text bounds and literal chat checks | `src/AmorRP.Core/Items/PlainText.cs`, `LiteralChatText.cs`, with compatibility helpers in `ItemText.cs` |
| Client prefix construction / game posting | `Plugin/Services/Game/ChatCommand.cs`, `GameChatSender.cs` |
| Stable validation hints / support view | `Plugin/Features/Groups/FormPresentation.cs` |
| Reusable summary table | `HoldingList.cs` in the same feature |
| Member roster and contextual actions | `MemberScreens.cs`; group/profile/history remain in `GroupScreens.cs` |
| Selected member item removal | `MemberInventoryScreens.cs` |
| Recipe list/editor and production | `CatalogScreens.cs`, `PotionScreens.cs` |
| Written forms and reader | `LetterScreens.cs` |
| New server behavior tests | `tests/AmorRP.Server.IntegrationTests/M5UsabilityTests.cs` |
| Matching candidate installation | `docs/delivery/m5-install-test.md` |

The plugin references both Contracts and pure Core. Package checks require both
DLLs and continue to reject host libraries. There is no Core-to-Contracts reference
or duplicate client/server message validator. No empty future feature directories
were added.

Plugin 0.0.6.1 removes `Features/Diagnostics/` and the M1 `IdentityProbeClient`.
`Features/Shell/MainWindow.cs` owns only product windows; login stays in GroupsPanel.
`Services/Api/XivAuthAuthorization.cs` retains the shared trusted-browser URL check;
`ProductServer.cs` owns the release origin in one place. Configuration no longer
contains a backend input setting. Server feasibility routes/tests remain unchanged
for historical compatibility; they are not exposed in the player interface.

## Custom testing distribution

`scripts/prepare-plugin-repository.py` validates the actual package and prepares
the feed/versioned assets. The explicit publish job in `plugin-ci.yml` promotes a
successful candidate to **main** alongside its source. The distribution files include
only distribution files; it is the deliberate exception to keeping build artifacts
with immutable per-version names. See [installation and publication](../delivery/plugin-repository.md).

The server callback presentation lives in `Server/Features/Authentication/Browser/`:
`LoginResultPage.cs` sets safe response headers and serves the embedded wallpaper,
`LoginResultPage.html` owns responsive styling and the close countdown, and
`Assets/` holds the supplied image and provenance. `LoginResultPageTests.cs` checks
callback/exchange behavior, nonreflection, CSP and bundled-asset integrity.

Plugin branding lives in `Plugin/Assets/Branding/`: the supplied original logo,
the full-logo square icon and its regeneration instructions. Package validation
checks the bundled icon dimensions and content-hashed feed URL; publication
copies those exact image bytes to the distribution branch's `images/` directory.

Group entry forms live in `Plugin/Features/Groups/GroupFormScreens.cs`.
`GroupFormWindow.cs` provides independently registered Join/Create windows;
`Plugin.cs` owns their WindowSystem lifecycle and MainWindow bridges the verified
GroupsPanel context. GroupInteractionState closes the matching window only after
confirmed success or receipt recovery. No duplicate forms remain in Settings.

The two-icon launcher is `Plugin/Features/Shell/LauncherWindow.cs`; Plugin owns
its registration and visibility. `Features/Groups/TradeInboxScreens.cs` runs
bounded cross-group reads and routes deliberate opening through the existing
trade window. `Services/Api/TradeInbox.cs` holds character/group-bound notification
state without game/UI dependencies. TradeInboxTests checks scope changes and
resolved/expired requests. No additional window content or icon image is needed
for the red badge.

`Features/Groups/TransientFeedback.cs` owns ordinary action/trade result expiration
and reopen cleanup, preserving durable unresolved-command recovery. TradeScreens
pumps automatic, group-scoped context-target resolution; settings navigation has
only the quick-launcher option. Ownership offers live with the Home profile.

Authentication/LoginPairingCode.cs generates and verifies browser-only one-time
pairing secrets; LoginBrowserPairing stores only the hash and persistent guess
count. Contracts/Auth/LoginExchangeRequest.cs defines the explicit handoff body.
Plugin FoundationClient separates status polling from user-triggered exchange.

Infrastructure/Security/ClientRateLimiting.cs defines network-scoped API/login
budgets and optional non-Render trusted forwarding. RenderClientIpMiddleware.cs
validates the public Render edge header and supplies only the rate-accounting IP.
Options/ReverseProxyOptions.cs validates ingress mode, trust ranges and hop count.
RateLimitTests.cs verifies the actual HTTP pipeline’s isolation and spoof handling.

Features/Authentication/LoginAdmission.cs enforces persistent originating-network
login occupancy/start budgets inside the auth transaction. LoginOriginCleanupWorker.cs
clears expired pseudonymous origin metadata. Infrastructure/Security/ClientNetwork.cs
owns address normalization shared with HTTP rate limits; SecretVault derives
purpose-specific keyed origin fingerprints. LoginAdmissionTests.cs covers capacity,
replays, concurrency/restarts, expiry, key rotation, cleanup and schema upgrades.

Features/History/HistoryEndpoints.cs contains HistoryService and route mapping.
Group-history reads use the shared active-membership/deleted-group guard; separate
operation receipt reads keep their actor/participant authorization. HistoryAccessTests.cs
covers inactive membership, moderation privacy, old cursors, deleted groups and
retained metadata recovery.

Features/Trading/TradeService.cs separates the short locked expiry transaction
from read-only snapshot response assembly. TradeService.Views.cs batches the four
projection lookups for a whole page and is shared with locked command responses.
TradeReadTests.cs uses PostgreSQL lock probes/EF command interception to verify
write progress, consistent snapshots, expiry durability, query bounds and privacy.

CategoryUpgradeTests.cs verifies that TradingBeta preserves owner category policy,
including seed-like labels, and tests owner-authorized narrowing without deleting
written assets. deploy/maintenance/review-category-types.sql is a read-only report
for already-upgraded databases, not an automatic policy rollback. Read operations
before using it; current values do not prove original owner intent.

## Resource/audit review additions — server 0.0.6.9

Within `src/AmorRP.Server/`:

- `Features/Health/ReadinessMonitor.cs`: one background database probe loop;
  `HealthEndpoints.cs` serves its cached result.
- `Options/HealthPolicyOptions.cs`: validated probe refresh interval.
- `Options/ServicePolicyOptions.cs`: retained creator budget alongside active caps.
- `Infrastructure/Persistence/Migrations/20261015201132_RetainedGroupCreationBudget.cs`:
  creator accounting/backfill, with matching designer and model snapshot.

`tests/AmorRP.Server.IntegrationTests/ResourceAuditTests.cs` covers resource
exhaustion, transfer/concurrency, migration and duplicate consent. There is no new
plugin feature or parallel backend structure.

## Member profiles

`src/AmorRP.Server/Features/Groups/GroupService.Profiles.cs` owns self-only description
writes; membership reads include the text. The matching `MemberDescriptions`
migration adds one membership field. `src/AmorRP.Contracts/Groups/MemberDescriptionEdit.cs`
contains the request and shared text limit. The plugin editor is isolated in
`src/AmorRP.Plugin/Features/Groups/MemberDescriptionScreens.cs`; `MemberScreens.cs`
arranges profile and permission sections. Server regressions live in
`tests/AmorRP.Server.IntegrationTests/MemberProfileTests.cs`.

## Member balance and permissions

`src/AmorRP.Plugin/Features/Groups/MemberCurrencyScreens.cs` isolates selection-bound
balance reads and the labeled vertical currency controls. MemberScreens contains
gameplay/moderator forms and Shift-button tooltips. CatalogService checks the
separate recipe-definition capability; MemberCapabilities carries the optional
positive trading flag for one atomic save. Permission regressions are in
`tests/AmorRP.Server.IntegrationTests/MemberPermissionTests.cs`. No new backend
folder or parallel permission system is introduced.

## Group Home and announcements

Contracts/Announcements contains create/read/list/unread DTOs and shared limits.
Server/Features/Announcements contains authenticated endpoints and bounded message/
read-state service. Persistence/Entities/AnnouncementRow and the GroupAnnouncements
migration store group-scoped posts and per-member read IDs. General/profile and
scoped grant checks remain in existing GroupService code.

Plugin/Features/Groups separates HomeProfileScreens, GroupSettingsScreens,
WeeklyPolicyScreens and AnnouncementScreens. Shell/NotificationBadge is shared
by the Home navigation and two-button launcher. Announcement polling uses its own
scope-checked task and never replaces forms or creates a full window reload.
HomeAnnouncementTests covers new service/permission invariants. These paths are
relative to the existing src project roots; no parallel backend/UI hierarchy exists.

## Multiline input and inventory layout

Plugin/Services/Ui/WrappedTextEditor.cs is the shared API 15 multiline editor.
WrappedTextProjection.cs separates visual wrapping from persisted text and maps
caret/selection/edit offsets (including Unicode and literal newlines). Projection
tests are linked into AmorRP.Plugin.Tests without native game dependencies.
Groups/FormPresentation.cs owns the shared editor instance; inventory/context
cleanup clears retained editing state. InventoryScreens.cs owns the shared
list/details view, fixed type-family registry and category → type → sort controls.
The registry is the single extension point for later inventory subtypes.

## Trade request navigation and launcher selection

Groups/TradeWindowScreens.cs draws the Inbound/Outbound request list and the
separate accepted-exchange layout. TradeScreens.cs owns selected request direction
and close/accept pumping; TradeInboxScreens.cs keeps cross-group request delivery
badge-only. Groups/StatusScreens.cs exposes one group selector for both the main
header and Shell/LauncherWindow.cs, bridged by MainWindow/Plugin. Selection uses
the existing SwitchGroup lifecycle; incoming invitations do not pin a group, while
sent requests and accepted exchanges retain cancellation-before-switch behavior.

## Home overview and logos

Plugin/Features/Groups/HomeOverviewScreens.cs owns responsive cards, logo picker
and display. HomeProfileScreens.cs owns inline profile/currency drafts and saves;
AnnouncementScreens.cs owns inline publishing and the full-message reader.
Plugin/Services/Media/GroupImageCache.cs is the shared scoped currency/logo texture
lifecycle. FoundationClient.GetImageAsync enforces streaming byte budgets.

Server/Features/Media/GroupLogoService.cs owns authenticated logo read/replace/remove.
StaticImageUpload.cs shares strict static decoding/normalization with CurrencyIconService.
GroupLogos migration adds purpose-specific media bounds and logo FK/version.
GroupLogoTests covers persistence, authority, input bounds, cleanup and upgrades;
GroupImageDownloadTests covers declared/chunked response limits.
