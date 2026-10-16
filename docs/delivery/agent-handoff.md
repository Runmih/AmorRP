# Current repository

Development and stable distribution use **main** at **Runmih/AmorRP**. Plugin
0.0.6.15 starts a fresh Git history, uses the supplied red-and-gold installer icon,
and introduces the simplified product README and plugin description. The public
feed is https://raw.githubusercontent.com/Runmih/AmorRP/main/repo.json. Server
remains 0.0.6.14; reconnect Render to this repository when migrating its source.
There is no new database migration or data wipe in this release. Use the approved
Runmih identity for every commit. Do not merge history from an older repository.

# Agent handoff

Current development uses **main** of
[Runmih/AmorRP](https://github.com/Runmih/AmorRP), based on accepted-for-progression
M4 commit `f661bd4`. Read the [M5 plan](m5-release-plan.md) first. Current installable
test packages are **plugin 0.0.6.12 / server 0.0.6.13**, the first usability checkpoint
with the login/permission cleanup patch. Read the
[M5 install/test guide](m5-install-test.md). The maintainer requested an
[custom Dalamud feed](plugin-repository.md); main holds source and distribution
only and the source workflow has explicit promotion. This is not completed M5 or approved 1.0.

```sh
git clone --branch main https://github.com/Runmih/AmorRP.git
```

Start with [M5 installation/test](m5-install-test.md), [documentation index](../README.md),
[API/OpenAPI](../engineering/api.md), [source map](../engineering/repository-layout.md)
and [versions](../engineering/versions.md). M3 is accepted per maintainer testing
on plugin 0.0.4.2. On 2026-10-15 the maintainer reported that the revised M4 trade
interaction feels better and requested M5. Do not infer completed production
operating drills or final 1.0 approval from that report. No production deployment
was performed by the build agent.

M4: vertical main navigation, horizontal subtabs, default Inventory, profile Home,
contextual authorized administration, History/devices in Settings, stable updates,
Books book/letter/note, CRUD membership deletion separate from blocking, canonical
holding reductions and persistent trading/reservations/revisions/atomic exchange.
Trading uses scoped polling, not the planned WebSocket endpoint. Source/OpenAPI
status is checked automatically. The first M5 checkpoint implements `<3`, field
feedback and clearer inventory/crafting/member workflows; final style review,
operations and export/deletion remain open.

The plugin patch **0.0.6.1** uses existing server **0.0.6.0**; no new server
deployment is needed if already on 0.0.6.0. This checkpoint has no
new migration after M4 DoubleTradeConfirmation. The plugin package now also
requires AmorRP.Core.dll for shared pure text validation. Literal `<3` and field
feedback are implemented; lifecycle/operations/release gates remain M5 work.
The separate trade window auto-saves arrow edits and currency on focus loss. Both
first approvals open final review; both final approvals transfer. Any edit after
either acceptance cancels the trade. Stale cancellation is allowed; approval is
revision-bound. Safely rebase peer changes only after a definite rejection; unknown
outcomes retain the original key. DoubleTradeConfirmation resets old pending
approvals and advances their revision, preserving offers/reservations and assets.
For the existing free Render service, keep manual deployment and all DB/XIVAuth/
vault settings. Set `AMORRP_MIGRATE_ON_START=true`; leave paid Pre-Deploy Command
and Docker Command overrides blank. The tracked starter Blueprint is not an
instruction to upgrade/provision the maintainer's free plan. Do not deploy, touch
production data or read secret values without the user's actual task authorization.

The additive TradingBeta migration preserves stable IDs, old content/balances,
grants/quotas and icons; adds trade tables/slots/lines and writing TypeId (existing
rows default letter). Server 0.0.6.8 removes its unsafe category name/type rewrites;
legacy policies remain intact and owners opt in explicitly. New groups seed Books.
No new login/provider setup is required. Existing assets never identify by name;
context name/world matches only the consented verified group roster. Owners have
implicit capabilities; quotas remain character/group-specific and alts independent.

Plugin 0.0.6.1 removes the entire startup chat permission flow, its checkbox and
the M1 tools. Signed-out UI is one login button; browser opening/completion is
automatic. ProductServer.Origin is the fixed live endpoint
`https://amorrp-server.onrender.com/`; no player editor or localhost default remains.
Never retag saved sessions/commands to a different origin. Same-key retries/receipt recovery
never auto-post game chat. Pending commands retain their protected original request.
No paid rename/world-transfer test or separate simulated-rename gate is required.
Testing must target meaningful accounting, access, upgrade and recovery behavior.

Use locked restore (without `-r`), then publish with explicit `-r linux-x64` for
Render. Native image decoding uses pinned SkiaSharp and bundled notices. Generated
plugin ZIPs and server binaries are artifacts, not source files to commit. The M5
guide records precise build/check commands and live test steps. Keep the tree and
contracts documented together when editing features.

Server **0.0.6.1** adds the styled XIVAuth browser result and the supplied wallpaper.
Deploy the server source on Render to see it; plugin 0.0.6.1 and minimum client
0.0.6.0 remain compatible, with no schema change. Successful pages attempt tab
closure after eight seconds with a browser-blocked fallback; failure pages remain
open. The image is embedded and served locally; no Reddit hotlink or provider
query/error data is rendered. See the latest [M5 deployment notes](m5-install-test.md).

Plugin **0.0.6.2** implements the maintainer's group-only header request and supplied
full-logo installer icon. Settings → Connection holds manual sync/status controls;
optional feedback below the content retains action errors, recovery and connection
failures without an idle Ready/details/dismiss panel. No server change is needed.
The original and square generated icon live in Assets/Branding; the publisher
validates/copies the bundled PNG and content-hashed IconUrl together with the feed.

Plugin **0.0.6.3** adds Join/Create group buttons beside the dropdown. Each opens
an independent registered GroupFormWindow; forms live in GroupFormScreens.cs
instead of Settings. Successful normal/recovered commands reset/close only the
matching form. Scope changes close/reset both; drafts survive normal refresh.
This is a plugin-only update with the same API/schema and server compatibility.

The maintainer also removed the main Trades tab in 0.0.6.3 before publication.
The dedicated trading window, context menu, member action and invitation handling
remain. MainSection enum now names navigation states rather than numeric indexes,
including the Settings-triggered history/device refresh checks.

Plugin **0.0.6.4** adds a titleless two-icon LauncherWindow, enabled by default and
controlled by Settings General or `/amorrp launcher`. The book opens main UI and
exchange arrows open trading. The maintainer explicitly chose **red badge only**
for incoming requests: remove the earlier automatic window opening. TradeInbox
binds requests to the verified character and joined groups; the selected group's
trade poll supplies its state and TradeInboxScreens polls other joined groups
about every 15 seconds. It runs with the main window closed. Opening another
group's request respects existing active-trade/unresolved-command guards.
Configuration.ShowLauncher is additive; config version 4 and protected data stay
compatible. No server/schema change. Actual Dalamud badges/placement require live
acceptance; 65 helper tests now include the cross-group/identity/expiry checks.

Plugin **0.0.6.5** simplifies Settings to General with only Show quick launcher.
The group header now has Join / Create group / Leave; owner leave is disabled
until ownership is transferred, and an active trade must end first. Ownership
accept/decline remains in Home. Connection, login/devices, history and support
panels and the Compact view button are removed, along with their unused reads;
server audit/session APIs and `/amorrp compact` remain. All hover tooltips are
removed. Ordinary action/trade feedback clears on main/trade reopening or after
10 seconds; uncertain operations retain their original recovery controls. Latest
terminal trades no longer reappear from polling. Context-menu Trade automatically
resolves the selected group's full verified roster, retries on group change and
only selects the stable character ID; it never sends an invitation itself.
No server update, dependency or migration is required. Live layout/selection and
two-character testing remain the maintainer's acceptance step.

Security review finding 1 is confirmed, reproduced and corrected in plugin
0.0.6.6 / server 0.0.6.2 (minimum client 0.0.6.6). Browser approval alone no longer
redeems. Callback generates a browser-only one-time pairing code; explicit
LoginExchangeRequest requires it alongside the attempt credential. Polling never
exposes the code. Guess limit is persisted, successful replay is code/body-bound,
and the plugin refuses an old server or automatic exchange after approval.
LoginBrowserPairing cancels legacy in-flight handoffs, preserving existing saved
sessions/assets. The callback no longer auto-closes. New pairing happens only
for a new device login, not normal refresh. Server redeployment and migration are
required; the build agent does not deploy Render. Read the newest security and
M5 deployment sections before installing/testing this coordinated update.

Security review finding 2 is confirmed. Server 0.0.6.3 partitions API/login
budgets; **0.0.6.4 completes Render support** by automatically selecting its
edge-overwritten CF-Connecting-IP header when RENDER=true. No static Render proxy
list or guessed hop count is required. Invalid/missing/duplicate headers fail
closed with 503; health is exempt. Direct/local hosting ignores IP headers unless
explicit generic proxy trust is configured. Render mode rejects mixed proxy lists.
Read operations for the public-ingress trust boundary and later hosted checks.

Infrastructure/Security/ClientRateLimiting.cs owns the budgets and generic
forwarding; RenderClientIpMiddleware.cs validates the Render rate key without
changing connection/authentication identity. ReverseProxyOptions.cs validates
mode and optional non-Render trust configuration. Local tests cover isolation,
spoofed prefixes, aliases, invalid headers and paired login; provider overwrite
is a documented infrastructure guarantee, not something local tests prove.

Plugin/minimum client remain 0.0.6.6; no new migration or plugin package. Render's
upstream DDoS protection remains; limits are per-process/network and require
shared enforcement before scaling replicas. No live restart/deployment was
performed or required to continue review/testing. Batch server rollout later.

Security review finding 3 is separate from HTTP bucket partitioning and is fixed
in **server 0.0.6.5**. PostgreSQL-backed LoginAdmission defaults to 5 unfinished
attempts/network and 10 new starts/rolling minute/network plus global cap 256.
The existing auth lock serializes count and insert across instances. Pending and
verified-unredeemed handoffs occupy slots; cancelled starts still count for the
minute. ClientNetwork shares Render-aware normalization with HTTP limiting.
Origin metadata is purpose-keyed HMAC, cleared after expiry by a minute worker;
key rotation counts retained keys. Read operations for retention/rotation details.

New migration **20261015191138_LoginNetworkAdmission** retires old unfinished
handoffs lacking origin data, preserving saved sessions/assets. Deploy every
server instance; no mixed old binary rollout. Plugin/minimum remain 0.0.6.6, with
no new plugin package, secret or Render setting. No live restart needed for local
tests or continued review. The original capacity bypass was reproduced before
correction; new tests cover durable isolation, lifecycle, retries, concurrency,
rotation, cleanup and migration. Batch deployment with the other review fixes.

Security review finding 4 is fixed in **server 0.0.6.6**. HistoryService in
Features/History/HistoryEndpoints.cs now uses GroupAccess.RequireAsync for both
feeds, requiring active membership/non-deleted group before cursor processing;
audit additionally requires owner. Dormant/blocked rows and old cursors no longer
grant group history, including later moderation reasons. The API/OpenAPI promise
of own history after leaving is replaced by separate metadata receipt recovery.
Existing operation-by-ID/by-key and terminal trade recovery remain scoped to the
actor/participant. A later moderation subject cannot read the owner's receipt.

No new migration or plugin package; plugin/minimum remain 0.0.6.6. Prior migrations
still apply. Regression reproduced leave/remove/block bypasses; tests also cover
old cursors, inactive unblock, personal receipts by ID/key, later moderation
receipt rejection, active filtering/audit and deleted groups. The earlier deletion
test now expects group-history refusal while keeping the personal receipt check.
No live restart or production change is required to continue reviewing.

Security review finding 5 is fixed in **server 0.0.6.7**. Trade reads formerly held
the group write advisory lock while doing per-line response queries. ReadAsync
now commits/disposes expiry under lock, clears tracked entities and assembles the
response in a read-only repeatable-read snapshot outside that lock. New
TradeService.Views.cs batches currency, lines, characters/authors and holding
metadata (four projection lookups per nonempty page). Command responses use the
same loader after saving, inside their existing locked transaction.

Regression reproduced list/single-read lock retention. Tests now prove another
connection can acquire the group lock and an actual offer edit completes while
projection is paused; the response keeps its original revision/quantities.
Other checks cover expiry persistence on rejected reads, reservation release,
query counts, cursor pagination, metadata/authors and sealed writing bodies.
Participant scope and revision/final-confirmation checks are unchanged. No new
migration/plugin package/Render setting; plugin/minimum stay 0.0.6.6. No live
restart is needed to continue review; batch the deployment with prior corrections.

Security review finding 6 is confirmed and corrected for future upgrades in
**server 0.0.6.8**. TradingBeta no longer renames or broadens existing categories.
There is no reliable M3 seed marker; narrowing by Correspondence/Books names alone
would still modify owner-created data. Preserve all category names/allowed types/
versions/retired flags instead. Fresh groups still get Books with book/letter/note.
Legacy M3 groups need owner-authorized category edits to enable new types/rename.

This edits historical migration SQL and adds no new migration ID. Already-upgraded
databases do not rerun it; do not claim a deployment restores existing policies.
Read deploy/maintenance/review-category-types.sql and operations: the report is
read-only and includes legitimate defaults/owner choices, not proof of corruption.
Existing active categories can be repaired through the owner-authenticated,
ETag/idempotency-protected category API after original intent is reviewed; the
current simplified plugin does not expose a general category-edit form. Do not
blindly remove book/note, reset migration history, or modify production data.

Tests reproduce broadening, verify mixed/letter-only/retired and seed-like custom
names survive the full upgrade, retain letter bodies/usage, confirm new defaults,
and demonstrate owner-only narrowing that preserves existing written items.
Plugin/minimum remain 0.0.6.6; no new secret/Render setting/plugin package. No live
restart or production repair was performed; existing policies require owner review.

## Security review finding 7 — retained resources and audit

Server **0.0.6.9**, plugin/minimum **0.0.6.6**. All three findings reproduced and are
addressed: immutable original-creator accounting caps retained groups at 30 by
default even across deletion/transfer; readiness is a cached no-store HTTP read
backed by one background probe per process; both approval stages reject repeated
new-key consent while preserving same-key recovery. See security/operations for
configuration, tests and cache latency.

New migration **20261015201132_RetainedGroupCreationBudget** backfills creator from
the original create actor, falling back to current owner only for missing evidence.
This does not change ownership or remove data. Deploy all instances together.
No plugin/feed promotion, production deployment, live restart, actual data wipe or
production database access was performed. Maintainer intends a test-data wipe
once review/work is complete; this is future intent, not an instruction to wipe now.

## Member profile UI — 0.0.6.7 / 0.0.6.10

User requested main navigation icons and less member-profile clutter. Added name,
group-specific description (self-only editing, 300 Unicode text elements) and
trade action; owner permissions are directly below. Other authorized currency,
items and membership actions remain collapsed. Background refresh preserves dirty
description drafts; concurrent description saves require reload. Owner self-grants
remain automatic. MemberDescriptionEdit carries the shared limit for UI/server.

Server **0.0.6.10** / plugin **0.0.6.7**, minimum **0.0.6.6**. New migration
**20261015203517_MemberDescriptions** adds empty text to membership rows. Deploy
matching server before profile testing; no live deployment or wipe performed by
this task. Source and plugin distribution retain the same branches/feed URL.

## Member controls — 0.0.6.8 / 0.0.6.11

Plugin **0.0.6.8** / server **0.0.6.11**, minimum client **0.0.6.6**. Labels above
currency/membership inputs, auto-loaded vertical balance with Refresh, positive
Trading and Craft Potions gameplay controls, Shift-enabled membership actions
with disabled-hover explanation, no confirmation checkboxes. Added Moderator
permissions section for the independent `items.potion.define` action. It authorizes
recipe creation/revision/retirement but not crafting, category/policy/grant or
membership administration. Gameplay and moderator saves preserve each other's
saved fields. Trading saves atomically with capabilities through optional
MemberCapabilities.TradingAllowed; null/omitted preserves current state.

No new migration/settings/dependency or production wipe/deployment. Server must be
updated before new permission saves. Ownership proposals are preserved when
trading is disabled; open trades still cancel and release reservations. Source
and plugin feed identity/URL are unchanged. See the focused M5 checklist.

## Group Home and announcements — 0.0.6.9 / 0.0.6.12

The user clarified permission naming: General Moderation is description/currency
name only. Manage Gameplay Permissions is a separate moderator action for Trading
and Craft Potions. Only the owner grants moderator actions. Announcements have an
independent Create Announcements grant. Do not conflate General Moderation with
crafting/grant authority or introduce a moderator role.

Plugin **0.0.6.9** / server **0.0.6.12**, minimum **0.0.6.6**. Crafting Info plus
access-filtered Potions; Home direct editing; five latest clickable announcements
with full-message dialogs and server read markers; Home/main-launcher badges;
owner Group Settings tabs; per-type weekly policy inputs loaded from active values.
Drafts survive polling. Each policy save preserves the other scheduled type.

New migration **20261015232913_GroupAnnouncements** adds bounded messages, group
sequence and per-membership read UUIDs. Create receipts omit message content;
read markers are idempotent metadata PUTs without operation rows or member ETag
churn. Notifications poll every 30 seconds while the main window is closed too.
Bodies/title bounds 5000/120, last five retained. See API/security/data-model and
focused M5 live checklist. No production deployment, data wipe or hosting changes
were performed by this task. Source/feed branches and URL remain unchanged.

## Inventory/text layout — plugin 0.0.6.10

Same server **0.0.6.12**. All inventory tabs use DrawInventory; fixed category
tabs hide the category dropdown and use a single type-family registry, while All
filters category before type/sort. Crafting displays only its own quota/reset on
two lines. No main-inventory pinned-only checkbox remains.

Services/Ui/WrappedTextProjection.cs maps visual line breaks to original text;
WrappedTextEditor.cs adapts API 15 multiline input with cursor/selection mapping,
bounded original-text undo/redo and clipboard normalization. All multiline
editors and the written-item reader use it. No soft breaks enter requests,
validation counts or saved documents. Eight regression cases cover the projection;
75 helper cases pass. Follow the live keyboard/resize checklist before accepting
this UI patch. No server code, data policy or schema changed.

## Trade request UI — plugin 0.0.6.11

Same server **0.0.6.12**. TradeWindowScreens splits Inbound/Outbound from the
accepted-exchange view. Requests use existing scoped inbox polling and fresh-read
acceptance; the offer/final-confirm protocol is unchanged. No manual recipient
dropdown: context menu or Members supplies the target, then Send request. Pending
requests survive closing; accepted exchanges still cancel on close, including a
local acceptance completing after closure. Incoming invitations alone do not block
group changes; sent invitations and accepted exchanges do.

StatusScreens owns the shared selector, bridged through MainWindow into LauncherWindow.
It retains pending-action/renewal guards, saved selection, notification annotations
and the active-trade group-switch safeguard. Live checks cover two-player accept,
close/accept, timer expiry, reopening requests and cross-group selection. No backend
or schema changes; existing 75 helper cases pass.

## Home overview/logos — plugin 0.0.6.12 / server 0.0.6.13

Deploy GroupLogos migration first. HomeOverviewScreens owns profile/cards/logo
picker and resource layout; HomeProfileScreens owns inline drafts/ETags. The
announcement composer is inline Title/Message/Publish for CanAnnounce. General
Moderation scope is unchanged; group name/logo stay owner-only. No edit icon.

GroupLogoService and StaticImageUpload reuse the existing authorized media flow;
logos have independent version/FK and Purpose=group_logo. Currency rows retain
Purpose=currency and original 128px/128KiB database bounds. Upgrades preserve old
assets; explicit downgrade removes only the new logo assets to restore old bounds.
GroupImageCache replaces CurrencyIconCache and handles both scoped textures.
FoundationClient streams image responses within explicit per-purpose byte limits.
New tests cover three formats, scaling, restarts, idempotency, independent versions,
cleanup/isolation/authority, invalid media and legacy migration preservation.

Plugin 0.0.6.13 / server 0.0.6.14: feedback is now a three-result footer with
20-second per-result countdown paused during details interaction. Group, character
and potion descriptions are capped at 300 Unicode text elements; Books bodies
(book/letter/note) at 500. Plugin typing/pasting and server writes enforce the
limits independently. Existing stored longer text remains readable; no migration.
Validation: 129 server/PostgreSQL tests, 83 plugin/helper tests and one contract
test passed (213 total); plugin Release build had no warnings/errors. Package and
feed validators pass. Replacement red RP icon arrived as `amorRPlogo.zip`; plugin 0.0.6.14
packages its exact 512×512 PNG bytes and uses a new content-hashed installer URL.
No server changes or redeploy are needed for the icon-only update.
