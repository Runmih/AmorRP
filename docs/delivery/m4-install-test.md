# M4 installation and focused live test

Build: **plugin 0.0.5.2 / server 0.0.5.2**, branch **m4-trading**, API v1,
Dalamud API 15. M3 was accepted on plugin 0.0.4.2. M4 changes **both** plugin and
server and includes **TradingBeta** plus **DoubleTradeConfirmation** migrations.
Update the server even if you already installed an earlier M4 build. M4 is a beta for live
acceptance; final styling and 1.0 operations remain M5.

## Get the packages/source

- [M4 source ZIP, including server, migration and instructions](https://codeload.github.com/Runmih/AmorRP/zip/refs/heads/main).
- [M4 branch](https://github.com/Runmih/AmorRP/tree/main).
- [GitHub Actions](https://github.com/Runmih/AmorRP/actions): choose the newest
  successful **Plugin package** run on m4-trading and download **AmorRP-plugin**.
  Its outer artifact ZIP contains **latest.zip**; extract that inner ZIP too.
- A successful **Server and contracts** run provides **AmorRP-server-linux-x64**
  with the published framework-dependent server. Render builds the source Docker
  image; that binary artifact is for operators using an installed .NET 10 runtime.

An expiring attachment is only a convenience; Actions/source links remain the
recovery path. These are manual test packages, not a public Dalamud repository.

## Upgrade the existing Render server first

Keep the existing database, XIVAuth application/callback, encryption vault key and
all environment variables. A new database/login/app is not needed.

1. Take a database backup using your existing operator/installation agent process.
2. In the existing Render service, choose branch **m4-trading**. Keep Dockerfile
   **deploy/Dockerfile**, repository root build context, and manual deployment.
3. On the **free** web-service plan, set **AMORRP_MIGRATE_ON_START=true**.
   Leave **Pre-Deploy Command** and **Docker Command** blank. The tracked container
   wrapper runs migrations before the server and stops if migration fails.
   No paid pre-deploy hook or plan upgrade is needed for this test setup.
4. Deploy that branch manually. Wait for **Database migrations applied.** and a
   healthy service. The new migration creates trades/slots/offer lines, preserves
   asset/character/category IDs and old content/usage. Server 0.0.6.8 corrects its
   original category rewrite: legacy names/allowed types remain unchanged; owners
   explicitly opt in to new writing types. New groups seed Books. Existing letters
   default to `letter`.
   DoubleTradeConfirmation adds final approvals and the confirming state. Existing
   pending first approvals reset and their revision advances; their offers,
   reservations and expiry remain. Terminal trades and assets are unchanged.
5. Open your existing `/health/ready` and `/api/v1/capabilities`. Readiness should
   be `ok`; serverVersion/minimumClientVersion should be **0.0.5.2**, and supported
   types should include potion, letter, book and note.

A free Render service may take time to wake after inactivity. If the first check
fails, wait for its startup logs and retry. Do not change credentials or erase data
because of a cold-start timeout. The paid starter Blueprint is a separate template;
these steps update your existing free service.

For a paid/external migration runner, leave startup migration disabled and run the
published server **with its existing connection/key environment**:

```sh
dotnet AmorRP.Server.dll --migrate
```

Then start normally. Source checkout operators may use
`dotnet run --project src/AmorRP.Server -- --migrate`. Migrations run on the server
host/runner, never from the plugin or inside FFXIV. Do not paste secrets in chat.

No production deployment was performed by the build agent. Once testers create
M4 writing/trades, use a backup/restore strategy for rollback; do not downgrade the
schema or discard retained assets/history. TradingBeta's Down migration rejects a
downgrade once new trade/writing data exists.

## Install the plugin

1. Disable Amor RP in Dalamud before replacing files.
2. Extract **latest.zip** into the same developer-plugin directory used for M3.1.
   Keep the accompanying AmorRP.Contracts DLL, DPAPI DLL and manifest/notices.
3. In Dalamud's developer-plugin settings, use the existing path to **AmorRP.dll**.
   Reload/enable the plugin. Verify **0.0.5.2** and Dalamud **API 15**.
4. Open `/amorrp`. Saved login, selected group and pins should remain. Answer the
   chat-consent question once for this startup; it is not repeated per potion use.
5. Inventory opens by default. Use **Settings → General** for server connection
   checks and group creation/join/leave. Use **Home** for the group profile and its
   owner controls. Authentication/devices and History are Settings subtabs.

`/amorrp compact` opens the separate consumables window. Old M3 layout tabs should
be gone; if they remain, the old DLL is still loaded.

## Focused two-character test

Use the existing owner and member, in the same test group. Keep test amounts small.
No paid name-change test or repeated full M3 checklist is required.

1. **Layout/stability:** verify vertical Home/Inventory/Crafting/Members/Trades/
   Settings and horizontal subtabs. Select an item/member, open a form, type a
   draft and scroll. Click Sync now and wait through one minute refresh. Selection,
   draft, tab and scroll should remain; reads should not freeze the whole window.
   An ordinary member sees only authorized administration buttons; no Owner tab.
2. **Short upgrade regression:** both retain their prior currency/items/grants.
   Create one permitted potion and one written item. Crafting → Books offers Book,
   Letter and Note, sharing the same weekly writing allowance. Existing letters
   remain readable. Use a potion and verify the chosen channel once.
3. **Invitation:** right-click the other player and choose **Amor RP: Trade**.
   Select the shared group, resolve the character against its verified roster,
   review the target, then Invite. On the recipient, expect a pending invitation
   and a separate trade window to open. Accept it. A character outside this group cannot
   receive that group's assets.
4. **Offer layout/normalization:** your inventory is on the left; your offer is
   top-right and the other player's is below. Right arrows add the chosen quantity;
   left arrows remove it. Enter **04** currency and leave the field: it should show
   **4**, save automatically and appear as 4 for the other player. No Publish or
   Reload button is required. Both may build offers concurrently. Check potion
   revision/message and written author; text contents remain sealed until receipt.
   Reserved amounts reduce availability and cannot be used or removed.
5. **Two-stage review/cancellation:** accept both offers. Nothing transfers yet;
   both receive a final popup with You send/You receive. Approve on one character
   and wait; the other need not click at the same instant. Assets stay reserved
   until both final approvals. In a separate attempt, accept on one character,
   then edit on the other: the whole deal must cancel and release reservations.
   Cancel or close the window during review to cancel it too. Cancellation becomes
   effective when acknowledged by the server; an already completed exchange is final.
6. **Completion:** start a fresh trade and complete both final popups. Check each
   receives the exact item/currency amount, reservations return to zero, total
   supply is unchanged, and received text is readable. Text stays locked against
   editing after its first trade. Settings → History identifies actor/affected
   player names/worlds and the item/currency summary without written bodies.
7. **Cancel/switch:** start another trade and reserve an item. Cancel; availability
   returns without refunding creation allowance. Start again and try a group
   switch; explicitly cancel/reconcile the trade before the new group's view opens.
8. **Recovery:** leave one trade open with an offer and reload one plugin or have
   the installation agent restart the test server. Reopen Trades: same trade,
   revision and reservations should remain. Cancel, or let its displayed expiry
   pass; the next poll/sweep releases reservations. If an action's outcome is
   unknown, use Retry original action/Check committed receipt with its saved key,
   never create a replacement trade to guess whether completion succeeded.
9. **Membership:** Remove a nonowner with a reason and confirmation; they may
   rejoin with a valid invitation, preserving assets/usage but needing fresh grants.
   Separately Block; joining must fail until Unblock. Unblock should leave them
   inactive until they join. Open trades cancel when either member is removed,
   blocked or trade-restricted. Owner can inspect summaries, not private text.
10. **Member item removal:** in Members inspect holding summaries, select one row,
    review name/quantity/reason, then remove the selected quantity. The selection
    and result should be clear and survive background reads.

Report the action, both displayed versions/revision, exact feedback/error code and
request/operation ID when available. Do not send tokens, invitation secrets or
private text. Literal `<3` recipe messages remain the agreed M5 follow-up.

## Automated verification and developer commands

The build checks accounting/privacy/isolation/revisions/reservations, replay,
expiry/restart, concurrent completion/removal, overflow/net-capacity and M2/M3
upgrade preservation using disposable **real PostgreSQL**. It does not prove
live Dalamud rendering, external XIVAuth availability or actual game chat delivery.

```sh
# SDK and packages remain pinned. Restore without a runtime override.
dotnet tool restore
dotnet restore src/AmorRP.Server --locked-mode
dotnet restore tests/AmorRP.Server.IntegrationTests --locked-mode
dotnet restore tests/AmorRP.Plugin.Tests --locked-mode
dotnet restore tests/AmorRP.Contracts.Tests --locked-mode

docker compose -p amorrp-m4 -f deploy/compose.yaml --profile test up -d test-database
export AMORRP_TEST_DATABASE='Host=localhost;Port=55432;Database=amorrp_test;Username=amorrp;Password=amorrp-local-only'
export ConnectionStrings__Database="$AMORRP_TEST_DATABASE"

dotnet build src/AmorRP.Server -c Release --no-restore
dotnet test tests/AmorRP.Server.IntegrationTests -c Release --no-restore
dotnet test tests/AmorRP.Plugin.Tests -c Release --no-restore
dotnet test tests/AmorRP.Contracts.Tests -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/AmorRP.Server --no-build --configuration Release
python scripts/check-docs.py
python scripts/check-server-startup.py

dotnet publish src/AmorRP.Server -c Release --no-restore -r linux-x64 --self-contained false -o artifacts/server-linux-x64
# With a verified official DALAMUD_HOME (Windows recommended for shipping):
dotnet restore src/AmorRP.Plugin --locked-mode
dotnet build src/AmorRP.Plugin -c Release --no-restore -p:Platform=x64
python scripts/check-plugin-package.py
```

Use the isolated test database only. Package/source checks and hosted CI evidence
are recorded with the release build; M4 live acceptance is pending the steps above.
M5 owns final styling, production backup/restore/capacity/support and lifecycle APIs.

## Build-agent verification record

Local Release verification: **107 tests passed** (54 server/PostgreSQL, 52 plugin
helpers, 1 contract boundary). Plugin/server compile with zero warnings/errors.
Locked restores, model parity, documentation (92 operations), seven startup-wrapper
scenarios and the actual plugin ZIP pass. The published linux-x64 server applies
DoubleTradeConfirmation through the startup wrapper; liveness, readiness and
0.0.5.2 discovery pass, and unauthenticated trading returns 401.

Verification for 0.0.5.2 checks real PostgreSQL accounting, two-stage
approval, leading-zero currency, stale final approval, cancel/expiry/restriction,
edit racing the last final confirmation, idempotent completion, restart recovery
and the additive M4 migration. Plugin helper checks cover normalization and draft
preservation when peer updates or in-flight saves arrive. Local/hosted results are
recorded with delivery. This does not establish live Dalamud rendering or game
acceptance. Repeat the focused two-character steps above.

## Earlier window movement correction — plugin 0.0.5.1

The maintainer's 2026-10-15 clip shows a short downward jump of the complete
form after potion and written-item creation, then a snap back. Ordinary saved
in-flight commands incorrectly displayed the unresolved-outcome warning, and
conditional Cancel waiting/feedback controls increased the height above the form.
This was layout movement, not evidence of an actual plugin unload/reload.

Plugin **0.0.5.1** puts the group/status controls and action feedback in fixed-height
regions. Cancel waiting keeps its reserved place; diagnostics open in a popup.
Normal running/queued writes show Saving action; recovery controls appear only
when the saved command is no longer running and still lacks a confirmed outcome.
The same protected retry key and receipt recovery are retained. Completed reads
return to Ready rather than leaving a Reading status behind.

The **0.0.5.1** correction was plugin-only. **0.0.5.2 requires the server
upgrade and migration described above**, and retains the fixed-height regions. Repeat the two creation clicks from the clip, switch
Potions/Books, type a draft, use Sync now and open/dismiss Action details. The form
should stay in place through request/feedback changes. Live confirmation remains
pending; build/package and existing recovery-helper checks validate the patch.

## Trading revision — plugin/server 0.0.5.2

The separate trade window owns local arrow/currency drafts and saves them without
manual publishing. An actual in-flight request may briefly disable duplicate
submission; stale cached revisions do not keep buttons locked. Peer offer changes
synchronize automatically without overwriting your draft. A change to your own
side from another client is shown explicitly for review rather than overwritten.
Unknown write outcomes retain the original request/key and receipt recovery.

Accept offer waits for your pending local saves. Both first approvals open final
review. The popup binds its immutable snapshot revision to final confirmation;
there is no rebase of final consent to a newer unseen offer. Any edit after either
acceptance cancels. The original expiry, default ten minutes from invitation, also
covers both confirmation stages and is not extended by edits or clicks.
