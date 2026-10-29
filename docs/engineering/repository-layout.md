# Repository layout and navigation

This is the current source map for 0.2.0.0. Plugin and server share a repository,
but build as separate applications. All paths below are relative to the repository.
See [architecture](architecture.md) for ownership and dependencies.

```text
src/
  AmorRP.Plugin/        Dalamud application
    Features/
      Communities/      Main feature views, drafts and selected context
      Shell/            Main window, trade launcher, favorite-events HUD
    Services/
      Api/              HTTP transport, command routes, request coordination
      Identity/         Protected saved sessions and pending commands
      Game/             Character/context/chat adapters
      Media/            Texture and image lifecycle
      Ui/               Wrapped text input and feedback queue
    Assets/             Bundled coin and plugin branding
  AmorRP.Server/        ASP.NET Core application
    Features/           Feature endpoints, services and workers
    Infrastructure/     Persistence, security and legacy wire adapter
    Options/            Validated deployment policies
  AmorRP.Core/          Pure text, crafting-type and capacity rules
  AmorRP.Contracts/     Request/response records grouped by feature

tests/
  AmorRP.Contracts.Tests/          Project boundary checks
  AmorRP.Plugin.Tests/             Linked production helper/coordinator tests
  AmorRP.Server.IntegrationTests/  HTTP/provider/database regressions

deploy/                 Docker, Compose, Render and maintenance scripts
scripts/                Reference download, documentation/package/feed checks
.github/workflows/      Linux server tests and Windows plugin packaging
docs/                   Current guides, specifications and dated release records
images/                 Content-addressed public plugin icon
packages/               Immutable versioned Windows plugin ZIPs and provenance
repo.json               Public Dalamud repository feed
```

## Finding a change

| Concern | Main sources |
| --- | --- |
| Composition and lifecycle | Plugin `Plugin.cs`; server `Program.cs` |
| Main view and selected character/community | Plugin `Features/Communities/CommunitiesPanel.cs` |
| Foreground/core request lifecycle | Plugin `Services/Api/RequestCoordinator.cs` |
| Independent feature refresh lifecycle | Plugin `Services/Api/BackgroundRefresh.cs`; `Features/Communities/FeatureRefreshScreens.cs` |
| Command response dispatch and refresh targets | Plugin `Services/Api/CommandRoutes.cs`; `Features/Communities/CommandCompletionScreens.cs`, `QuietUpdates.cs` |
| Treasury balances, transfers, records and disband returns | Plugin `Features/Communities/GroupTreasuryScreens.cs`; server `Features/Currency/TreasuryService.cs`, `TreasuryLedger.cs`, `CurrencyAccounts.cs`; `Features/Groups/GroupLifecycle.cs` |
| Group staff permissions | Plugin `Features/Communities/GroupPermissionScreens.cs`, `GroupPermissionDraft.cs`; server `Features/Groups/GroupAuthorization.cs`, `GroupService.Permissions.cs` |
| Group profile draft | Plugin `Features/Communities/GroupProfileDraft.cs`, `NestedGroupScreens.cs` |
| Trade draft and views | Plugin `Services/Api/TradeDraft.cs`; `Features/Communities/TradeScreens.cs`, `TradeWindowScreens.cs` |
| Favorite events HUD | Plugin `Features/Shell/FavoriteEventsWindow.cs`; `Features/Communities/FavoriteEventsHud.cs`, `FavoriteEventSchedule.cs` |
| Community profile, announcements and settings | Plugin `HomeProfileScreens.cs`, `AnnouncementScreens.cs`, `CommunitySettingsScreens.cs`, `WeeklyPolicyScreens.cs` under `Features/Communities/` |
| Authentication and ownership verification | Server `Features/Authentication/AuthenticationService.cs`, `DurableXivAuthProvider.cs`, `SessionAccess.cs` |
| Community membership and grants | Server `Features/Communities/CommunityAccess.cs`, `CommunityService.*.cs` |
| Group ownership, joins, private content and events | Server `Features/Groups/GroupService.cs`, `GroupActivityService.cs`, `GroupEndpoints.cs` |
| Recipes, production and holdings | Server `Features/Catalog/`, `Features/Inventory/`; Core `Items/CraftingTypes.cs` |
| Trade transactions and projection | Server `Features/Trading/TradeService*.cs`, `TradeReservations.cs` |
| Database mapping and migrations | Server `Infrastructure/Persistence/AmorDbContext.cs`, model files, `Migrations/` |
| Request fingerprints, locks and receipts | Server `Infrastructure/Persistence/CommandStore.cs` |
| Provider and deployment secrets | Server options and deployment environment; never committed values |
| Release endpoint and metadata | Plugin `Services/Api/ProductServer.cs`, plugin/server project versions, plugin `AmorRP.json` |

`*Screens.cs` files are parts of one `CommunitiesPanel` class. Splitting a file does
not by itself isolate its state. Reusable draft/request components own their own
state and are compiled directly into helper tests.

## Change and test placement

Put server rules that require database state beside their transaction in the
corresponding feature service. Put reusable pure rules in Core, transport records
in Contracts, and local UI/request behavior in Plugin. Do not move authoritative
permissions or balances into the plugin.

For a new plugin mutation, register its exact verb/path shape, operation, result
handler and refresh targets in `CommandRoutes`. Add its result behavior to the
matching completion handler. Server group activities expose typed entry methods;
the private receipt envelope handles shared locking and persistence.

Use PostgreSQL integration tests for authorization, transactions, rotation, receipts
and concurrency. Use the linked production components in plugin tests for draft
and asynchronous-state regressions. UI layout and native game behavior need manual
checks. Build/test commands are in [development](../delivery/development.md).

## Distribution and historical records

The normal feed is on `main`; package URLs and source/build provenance are immutable
per version. `testing` support exists in the publication script, but there is no
separate hosted testing server in the current deployment. Do not publish server-
incompatible testing features against the shared stable service.

Older `m0`–`m5` delivery files describe their dated milestone checkpoints. Planned
folders, old version numbers and previously pending CI gates in those records do
not describe today's repository. Current guides are this page, architecture,
authentication, development and the latest release record linked by the docs index.
