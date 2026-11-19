# Agent handoff

Read [AGENTS.md](../../AGENTS.md), the [milestone plan](../planning/README.md),
[current rules](../product/rules.md), [source map](../engineering/repository-layout.md)
and [development workflow](development.md). Work in Runmih/AmorRP on main.

Seasons are the only environment model. Every verified character belongs to every
non-deleted season; membership has no join/leave/removal/block state. One season is
active, other economies stay separate and read-only. `seasons.create` is global and
is automatic only for the privately configured Lodestone administrator. Hide
unauthorized controls and enforce server-side authorization.

Current source version is inherited from Directory.Build.props. ApiProtocol holds
the protocol/minimum version boundary. repo.json and packages/*.json record the
published exact Windows package and source commit. Do not infer deployed home-server
version from source or change the private host while doing local maintenance.

The shell owns separate feature windows; SeasonsPanel partials share current view
state. Settings is Interface/Account/About. Preserve window IDs, saved property
names, DPAPI purposes and origin bindings. The launcher book opens Writing; its
power icon controls automatic panels/HUDs. Do not reintroduce the old main menu.

Server feature services own transactions, database locks, reauthorization, ETags
and receipts. Shared media code handles storage/hash/PNG response only. Financial
mutations use CurrencyAccounts/DebtSettlement/TradeReservations. Do not hide lock
ordering or authorization inside a generic engine.

S01 and S02 passed maintainer game checks. S03, Seasons and dedicated-window layout
still need the focused [manual checks](acceptance.md). Planned S04–S11 features,
export/deletion and final operating drills are not implemented/accepted by this
maintenance pass. Do not claim new game checks or a production restore.

Use the Runmih commit identity, locked dependencies and disposable PostgreSQL tests.
Preserve migration history and immutable packages. Both source workflows must pass
before publishing the exact Windows artifact. All main pushes now run checks, including publication. The built-in-token publisher
also dispatches them explicitly; the home updater verifies latest main.
