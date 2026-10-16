# Install and test M3

M3 is a testable inventory alpha, not 1.0. Plugin **0.0.4.0**, server **0.0.4**,
Dalamud API **15**, API **v1**. Use branch `m3-inventory`. Existing M2 identities,
saved device sessions, groups and balances are preserved; do not wipe the database
or regenerate the encryption key. This build includes the M2 member-removal fix;
it still requires the in-game check below before acceptance.

## Included

- A shared group dropdown above the tabs; active members by default and owner-only
  **Show inactive members** for retained dormant/blocked memberships.
- **Refresh data**, last-update/stale feedback and a 60-second read refresh while
  either product window is visible. Refresh preserves policy, recipe and letter
  drafts. Hidden windows do not poll. Startup chat consent does not reset.
- Member removal has a dedicated required reason and confirmation. Action results
  survive refresh; **Action troubleshooting** shows safe HTTP status, error code,
  request ID and operation key. Rejected kicks do not create a success history event.
- Successful joins clear the invitation/visibility fields; successful group
  creation resets its own form. Failed or unknown outcomes retain input.
- Owner currency-icon upload for static PNG, JPG/JPEG and WebP, max 128x128 and
  256 KiB encoded by default. Server validates/normalizes to PNG and stores it in
  PostgreSQL; a bundled coin icon is used when absent. Icon edits preserve balances.
- Default Consumables/Correspondence categories for old and new groups; owner
  category creation/rename/retirement and immutable potion recipe revisions.
- Authorized potion creation spends the creator's independent weekly points;
  ordinary characters may create letters within their own allowance. Owners also
  spend their own allowance. No refill on rejoin, discard or grant toggling.
- Searchable, paginated inventory with type/category/sort controls; private letter
  reading and eligible editing; quantity-confirmed discard and authorized removal
  through holding summaries that exclude letter bodies.
- Compact consumables, per-group/character pins, exact message/destination preview,
  local party/linkshell membership checks before and after consumption, deliberate
  use of one potion, and explicit manual message resend after review.
  Replayed/recovered consumption never automatically posts chat.

Trading is M4. Cosmetic polish, operational/privacy/backup acceptance and official
plugin distribution are later gates. The old M1 tools remain diagnostic only.

## Upgrade Render

1. Back up the existing RP test database. In the existing service, change the
   deploy branch to **m3-inventory** and deploy manually. Keep root build context,
   `deploy/Dockerfile` and `/health/ready`. Choose the migration method for the
   service's compute plan:

   - **Free web service:** pre-deploy commands are unavailable. Leave both
     **Pre-Deploy Command** and **Docker Command** empty, add environment variable
     **`AMORRP_MIGRATE_ON_START=true`**, and deploy the latest `m3-inventory` commit.
     The container applies pending migrations, then starts the API only if they
     succeed. Logs should show **Database migrations applied.** before server
     startup. No server installation on your PC is needed.
   - **Paid web service:** keep the separate pre-deploy command
     **`dotnet AmorRP.Server.dll --migrate`** and leave
     `AMORRP_MIGRATE_ON_START` unset or `false`.

   Render documents the paid-only pre-deploy restriction in its
   [deployment guide](https://render.com/docs/deploys#pre-deploy-command).
   Startup migration is opt-in and intended for single-instance testing. It runs
   on every cold start but applies only migrations not recorded as complete. It
   does not reset the database. A failed migration prevents the new API from
   starting; inspect deploy logs before retrying. Use a separate migration step
   before scaling or deploying destructive/schema-incompatible changes. The
   tracked `deploy/render.yaml` remains a paid-service template.

2. Keep the existing PostgreSQL connection, XIVAuth client/callback/scopes, and
   `SecretVault` keys. M3 adds no authentication setup. Its additive migration
   creates categories, revisions, holdings, letters, usage snapshots and media;
   existing currency IDs, balances, memberships and sessions remain intact.
3. Check `/health/ready` and `/api/v1/capabilities`: server/minimum client `0.0.4`,
   supported types `potion` and `letter`. Do not mix an M3 plugin with an M2 server.
   If a restore is needed, restore the backup and matching M2 server together;
   rolling back only the binary leaves an incompatible newer database.

The maintained SkiaSharp **4.153.1** decoder and Linux native runtime are pinned in
package locks. Server publication includes its MIT and third-party notices. Media
is in the database, not Render's ephemeral filesystem. Optional validated service
settings include `ServicePolicy__MaxCurrencyIconBytes` (262144 default), category
and definition ceilings, inventory capacity and existing text/quantity limits.
The icon dimension ceiling for this release is 128 and is advertised in capabilities.

## Install the plugin

1. Close or unload the M2 development plugin first. Extract
   `artifacts/AmorRP-M3-plugin.zip` into a fresh local plugin folder.
2. Keep all files together: `AmorRP.dll`, `AmorRP.Contracts.dll`, ProtectedData DLL,
   plugin manifest and license notice. Do not copy new DLLs over a loaded plugin.
3. In Dalamud Settings → Experimental → Dev Plugin Locations, select the extracted
   `AmorRP.dll`, then load it in the plugin installer **Dev Tools** tab.
   Remove the old development location so only one Amor RP instance loads.
4. Open `/amorrp`. Keep the existing HTTPS backend address. Saved M2 login should
   resume on the same Windows user; a new device still needs its own initial login.
   Answer the startup chat question once and use **Check connection**.
5. Open **Open compact consumables**, or `/amorrp compact`, to toggle the small
   window. It shares login, selected group and inventory state with the main window.

If no groups appear, confirm the server is M3, login matches the current character,
and there is no unresolved saved action. Use Refresh data before reloading the
plugin. Changing character/group/backend clears old private views.

## Focused in-game acceptance

Use an owner and two member characters. Record plugin/server versions and safe
request/operation IDs for failures; keep tokens, invitation codes and letter bodies
out of bug reports. No paid rename or world-transfer test is required.

### First: the carried M2 kick

1. Owner selects a member. Leave **Member removal reason** empty: removal must be
   disabled. Enter a reason and check the block confirmation, then remove.
2. Confirm the active roster hides that member, **History & devices** has exactly
   one removal entry, and the removed character loses group/inventory access after
   its next refresh. Its assets remain retained internally.
3. Owner enables Show inactive members: the blocked row appears. Restore eligibility
   with a reason; rejoin using an invitation, then reauthorize potion creation as
   needed. Existing holdings/usage must return, without a new allowance.
4. If removal is rejected, copy only **Action troubleshooting**: HTTP status, code,
   request ID and operation key. The error must remain visible across Refresh data.
   A stale version requires review/retry after refreshing; there is no silent retry
   with a fresh operation key.

### Navigation, refresh and forms

- Switch groups from every tab and the compact window; verify group/character and
  currency always match. Old letter details must disappear on a switch.
- Confirm dormant members are hidden until the owner opts in. Ordinary members
  should never see inactive rows.
- Join successfully: entered code/visibility reset. Create successfully: its draft
  returns to defaults and the new group is selected. On rejection inputs remain.
- Leave a policy/recipe/letter draft open while another character changes inventory;
  manual refresh and the next minute update read data without erasing the draft.
  Hidden windows stop polling. Sign-out or switching never reasks startup consent.

### Currency image

Upload a static PNG/JPG/WebP no larger than 128x128 from owner settings. Check both
members see the icon and balance. Replace it, revert to default and restart the
server: currency identity and balance stay the same. Oversized/animated/malformed
images must fail clearly; ordinary members cannot change icons.

### Inventory play session

1. For immediate testing, create a group with **12 initial potion points** and
   **5 letters**. Existing group policy edits apply next Monday, so a zero-point
   group cannot gain an immediate allowance through editing its current policy.
2. Owner defines a potion at difficulty **3**, with a short literal use message.
   Authorize each member for potion creation. Each character should independently
   be able to create four copies; a fifth exceeds that character's 12 points.
3. Change the recipe cost/message. New copies use the new revision; old copies
   retain their original description/message. No old spending is recalculated.
4. Each member writes and reads a letter. Only its holder can see the body; owner
   management shows a summary. Edit while author still holds an untraded letter.
   A sixth letter in that week fails. Discarding does not refund usage.
5. Try inventory filters, pagination, pinning and both window sizes. Preview a
   potion message, select Say/Emote/Party/an intended linkshell, then Use once.
   Count drops by one and exactly one message is submitted. Declining startup
   consent disables use; permitting it in settings enables use without another popup.
6. Refresh/reopen/restart: holdings, letters, currency and usage remain. If a use
   times out, reconcile its saved action before consuming again; a recovered/replayed
   response must not post automatically. Manual resend requires its duplicate warning.

M3 is accepted after these live checks pass, including the kick. Visual feedback is
welcome for functional usability now; final styling remains M5.

## Developer reproduction

Use the pinned SDK and fetched Dalamud references in [the version matrix](../engineering/versions.md).
The server artifact targets current Render Linux x64; choose `win-x64` or
`linux-arm64` explicitly for those supported developer targets. Run only against
a disposable database named `amorrp_test*`:

```sh
dotnet restore AmorRP.sln --locked-mode
dotnet tool restore
dotnet build AmorRP.sln -c Release --no-restore
docker compose -f deploy/compose.yaml --profile test up -d --wait test-database
export AMORRP_TEST_DATABASE='Host=127.0.0.1;Port=55432;Database=amorrp_test;Username=amorrp;Password=amorrp-local-only'
dotnet test AmorRP.sln -c Release --no-build --no-restore
export ConnectionStrings__Database="$AMORRP_TEST_DATABASE"
dotnet ef migrations has-pending-model-changes --project src/AmorRP.Server --configuration Release --no-build
python scripts/check-docs.py
python scripts/check-server-startup.py
dotnet build src/AmorRP.Plugin -c Release -p:Platform=x64 --no-restore
python scripts/check-plugin-package.py src/AmorRP.Plugin/bin/x64/Release/AmorRP/latest.zip
dotnet publish src/AmorRP.Server -c Release --no-restore -r linux-x64 --self-contained false -p:UseAppHost=false -o artifacts/server
python scripts/package-milestone.py --milestone M3
```

## Verification record

On 2026-10-07 the verification passed **76 tests** (38 server/PostgreSQL,
37 plugin helper, 1 shared-assembly boundary). Meaningful M3 additions exercise
concurrent allowance accounting, same-key use replay, immutable recipe revisions,
independent character budgets, rejoin/week rollover, letter quota/privacy/editing,
reasoned kick/history/access loss, all three image formats/isolation/persistence,
ordered inventory pagination and an M2-data upgrade. Three further client checks
exercise lost-response/replay/rejection handling: an earlier uncertain attempt
keeps its original key even if a later retry is denied. All 37 plugin helper tests
passed after that change; the server/contract checks are independent.

Locked restore, plugin build/package checks, model/migration validation and current
OpenAPI/documentation validation are required before packaging. A server dependency
scan reported no known vulnerable packages in the current NuGet feed. This record
is not in-game acceptance or hosted CI/container evidence.

Published Linux x64 native smoke checks also passed: real HTTP group/recipe/
production/use/replay, authenticated normalized PNG, a valid upload larger than
the JSON body ceiling, APNG rejection and encoded-size rejection. The disposable
smoke database/process were removed afterwards. Docker build was attempted but
blocked when Microsoft's registry redirected to `centralus.data.mcr.microsoft.com`,
which this workspace's network policy forbids. Record Render/CI container results
separately; no hosted deployment was performed from this workspace.

On 2026-10-14, seven container-wrapper checks verified opt-in migration before
startup, failure preventing API launch, explicit migration without starting an
API, argument forwarding, configuration validation and direct server termination
signal handling. These use a fake `dotnet` process to exercise the real shell
wrapper; they do not represent another database migration or Render deployment.
