# Repository layout and navigation

Paths are relative to the repository. Source version comes from Directory.Build.props;
repo.json identifies the published package. [Architecture](architecture.md) explains
state ownership, dependencies and transaction boundaries.

| Location | Purpose |
| --- | --- |
| src/AmorRP.Plugin/Features/Shell | Settings shell, launcher and dedicated workspace windows |
| src/AmorRP.Plugin/Features/Seasons | Shared selected context, feature screens, drafts and refresh state |
| src/AmorRP.Plugin/Features/Inventory | Inventory queries and compact inventory tools |
| src/AmorRP.Plugin/Features/Writing | Writing log snapshot and text helpers |
| src/AmorRP.Plugin/Features/Trading | Trade window host |
| src/AmorRP.Plugin/Services/Api | HTTP client, command routes, compatibility gate, request coordination and refresh helpers |
| src/AmorRP.Plugin/Services/Identity | DPAPI sessions, pending commands and local identity helpers |
| src/AmorRP.Plugin/Services/Game | Character/context/chat adapters |
| src/AmorRP.Plugin/Services/Media | Texture/image lifecycle |
| src/AmorRP.Plugin/Services/Ui | Wrapped text projection/input and feedback helpers |
| src/AmorRP.Server/Features | Feature HTTP endpoints, business services and workers |
| src/AmorRP.Server/Infrastructure | Persistence, command receipts, security, readiness and proxy/rate limiting |
| src/AmorRP.Server/Options | Validated deployment policies |
| src/AmorRP.Core | Pure text/chat/type rules |
| src/AmorRP.Contracts | Transport shapes and ApiProtocol |
| tests/AmorRP.Contracts.Tests | Project dependency boundaries |
| tests/AmorRP.Plugin.Tests | Linked production helper/coordinator/draft tests |
| tests/AmorRP.Server.IntegrationTests | HTTP/provider/accounting/upgrade tests against disposable PostgreSQL |
| deploy/home | Private host Compose, configuration generator and verified build helper |
| scripts | Reference fetch, docs/startup/deployment checks and package/feed validation/publication |
| docs/planning | Single milestone plan and accepted future feature requirements |
| packages | Immutable published ZIPs, digests and source/build provenance |

## Common changes

| Task | Start here |
| --- | --- |
| Season inclusion or lifecycle | Server Features/Seasons/SeasonMemberships.cs, SeasonService.cs and SeasonAccess.cs |
| Global creation rights | SeasonService.cs, Contracts/Seasons/Seasons.cs and plugin SeasonCreationAccessScreens.cs |
| Recipe/inventory rules | Server Features/Catalog and Features/Inventory; Core/Items |
| Personal money, treasury or debt | Features/Currency, Features/Groups/GroupLifecycle.cs, Features/Billing |
| Trade state/reservations | Server Features/Trading; plugin TradeScreens/TradeWindowScreens and API draft helpers |
| Authentication | Features/Authentication; provider policies under Features/Feasibility; engineering/authentication.md |
| Images | Features/Media/StaticImageUpload.cs and MediaAssets.cs; feature-specific image service callers |
| HTTP command recovery | CommandStore in Infrastructure/Persistence; plugin CommandRequest/CommandRoutes and pending-command state |
| Shared window appearance | Plugin Services/Ui/PluginTheme.cs, applied once around WindowSystem.Draw |
| Launcher sizing/toggles | Features/Shell/LauncherWindow.cs; open/close wiring in Plugin.cs |
| Client/server versions | Directory.Build.props, Contracts/Common/ApiProtocol.cs, plugin ServiceCompatibility.cs and server capabilities |
| Package publication | .github/workflows, prepare-plugin-repository.py and check-plugin-repository.py |
| Deployment/update | deploy/home/prepare-update.py, compose.yaml and delivery/home-server.md |

## Maintenance rules

Keep services beside their feature and preserve explicit financial/authorization
ordering. Extract concrete repeated operations; do not introduce a generic command
engine or empty future modules. Preserve applied migrations and the storage names
that map existing records. Preserve saved JSON keys, DPAPI purposes and window IDs.

Historical CommunityId/communities names in persistence and transport backing
properties are mapped storage identifiers, not an alternative product model. The
current wire API uses seasons. Retired milestone packagers, request conversion and
testing-branch publication paths are removed. Earlier detailed documents remain
available through Git history; current instructions start at the [documentation index](../README.md).
