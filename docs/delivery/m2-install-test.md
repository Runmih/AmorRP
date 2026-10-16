# Install and test M2

M1 passed per the maintainer's report on 2026-10-07. M2 is the next test build:
plugin **0.0.3.0**, server **0.0.3**, Dalamud API **15**, API **v1**.
Use branch `m2-foundation`. The existing M1 backend/database can be upgraded;
no database wipe is required. The older temporary login does not become a saved
product session: sign in once through the new Character login section.

## What is included

- Saved per-character login, automatic ownership revalidation and rotating
  credentials; Windows CurrentUser DPAPI protects the local save.
- Up to 3 owned / 6 active joined groups per verified character, owned included.
  Different characters, including alts, remain independent.
- Explicit group visibility acceptance, expiring/revocable invitations, roster,
  leave, removal/blocking, restoration/rejoin, ownership proposal/acceptance,
  exact-name group deletion and a persistent group selector.
- Direct grants for `currency.manage`, `items.potion.create`, `inventory.remove`.
  The latter two configure future M3 behavior; they do not create inventory yet.
  Trading defaults allowed and can be restricted; actual trades arrive in M4.
- Named group currency, whole-unit balances, reasoned adjustments and history.
  No currency balance or membership is selected by a display name.
- Weekly policy defaults and scheduling: changes apply next Monday 00:00 UTC.
  UI shows current/scheduled values and reset time. Quota spending arrives in M3.
- PostgreSQL locking, safe operation retries, ETags and cross-group checks.
  A retried adjustment cannot mint/debit twice. Credentials and unresolved retry
  keys are saved before renewal/commands. Startup chat permission stays as agreed.

There is no inventory, potion/letter creation, item usage or trade workflow in
this milestone. Category seeds are deferred to the M3 inventory migration.
The old M1 tests remain under **M1 diagnostic tools**.

## Upgrade the backend on Render

1. Back up the dedicated RP test database. Change the service's branch to
   `m2-foundation`; retain its Dockerfile `deploy/Dockerfile`, root build context,
   database secret and pre-deploy command `dotnet AmorRP.Server.dll --migrate`.
   Deploy manually. M2 adds a reviewed additive migration with characters,
   attempts/sessions, groups/members, invitations, ownership and operation ledger.
2. In your existing XIVAuth confidential OAuth client, enable the minimal scopes
   **character** and **refresh** for authorization-code login. Register the exact
   new callback `https://YOUR-SERVICE/auth/xivauth/callback`. Keep the old callback
   only if you want the M1 diagnostic login as well. Do not request character:all,
   user/email, manage or game account scopes.
3. Generate a 32-byte random encryption key **locally**; do not post it in chat.
   PowerShell:

   ```powershell
   $rpKeyBytes = [byte[]]::new(32)
   [System.Security.Cryptography.RandomNumberGenerator]::Fill($rpKeyBytes)
   [Convert]::ToBase64String($rpKeyBytes)
   ```

   POSIX alternative: `openssl rand -base64 32`.
4. Store these in Render's secret/environment settings:

   ```text
   XivAuth__DurableEnabled=true
   XivAuth__ClientId=<existing confidential OAuth client ID>
   XivAuth__ClientSecret=<client secret>
   XivAuth__DurableCallbackUrl=https://YOUR-SERVICE/auth/xivauth/callback
   SecretVault__ActiveKeyId=primary
   SecretVault__Keys__primary=<generated base64 key>
   ```

   `XivAuth__Enabled` controls only M1 probes; it may be false. When true, its
   existing `XivAuth__CallbackUrl` must still be configured. Never regenerate the
   vault key on ordinary redeploy: existing grants/replay records need it. Keep
   it in your secure backup alongside database recovery instructions. Rotation
   adds a new key ID, switches ActiveKeyId and retains the old entry.
5. Restart/redeploy after configuration changes. Check `/health/live`,
   `/health/ready` and `/api/v1/capabilities`: server 0.0.3, minimum client 0.0.3,
   three grantable actions. Disabled/malformed auth or key configuration cannot
   simulate a successful login. Invalid enabled configuration prevents startup.

The new provider scope and renewal must be checked live. If XIVAuth rejects them,
record the safe error and fix the adapter/configuration before accepting M2.
This task does not have your external service credentials or deploy on your behalf.

## Install the plugin

Unload/disable the existing plugin. Extract **all files** from
`AmorRP-M2-plugin.zip` into its development-plugin folder, replacing the M1 files.
Keep all DLLs, manifest, .deps.json and dependency license notice together. Follow the [M0 development loader instructions](m0-install-test.md#install-the-plugin-artifact)
if this is a new machine. Reload and run `/amorrp`; the title identifies M2.
Your saved backend origin is retained. Set it to the upgraded HTTPS service and
check the connection. Use the new **Sign in with XIVAuth** and **Open XIVAuth in
browser**, verify the character, then return to the game. The login saves automatically.

On normal reload/restart the plugin resumes saved login without another browser
consent. Open the window after a restart to resume; this M2 UI renews while open
and renews on the next open if necessary. Refresh validity defaults to 30 days
since last renewal. A long absence, logout/revocation, lost save, reinstall, new
Windows user/machine or provider grant failure can require browser login again.
Those events do not replace the character or erase group assets.

Character switches use independent saved logins. After a rename/world transfer,
choose **Use a saved identity** and the corresponding saved character; resume or
reauthenticate using its stable ID. This does not need a paid live test. If the
verified provider profile is stale, the UI refuses a mismatch and preserves assets.
Numeric world IDs are local display hints; provider ID/key controls ownership.

## Manual M2 exit checklist

Use two verified characters and two isolated test groups. More characters can
exercise join caps, but the automated PostgreSQL tests already check concurrency
at the agreed 3/6 limits. Do not buy a rename or world transfer for this milestone.

1. Sign in, create Group A with a named currency and initial weekly limits, and
   Group B with visibly different names. Switch between them: names, your balance,
   roster/history and owner settings must follow the selected group.
2. Create an A invitation. The other character accepts visibility and joins A.
   Confirm its roster entry. Ordinary members cannot invite, edit policies,
   grant permissions or adjust currency. Group B remains inaccessible until
   separately invited; its balances must not appear in A.
3. Authorize the member's Manage currency action. Adjust A currency with reasons,
   positive and negative deltas. Verify both balances and owner/personal history.
   An excessive debit is rejected without changing the balance. Revoke the action;
   further adjustments must be denied immediately.
4. Toggle the member's trading restriction. Confirm it displays in that group
   only. Potion and removal grants can be saved, but no inventory operation exists.
5. Schedule weekly policy changes. Verify the current limits remain, the next
   limits update, and the UI shows next Monday's reset in local time. The boundary
   is covered by an automated server-clock test; do not wait a week to accept M2.
6. Remove/block the member. It loses group access and cannot rejoin with a code.
   Restore eligibility and rejoin with a still-valid invitation: old balance stays,
   action grants remain cleared. Leave/rejoin also preserves balance.
7. Propose ownership to that member. Only the recipient can accept. Confirm the
   former owner stays a normal member and loses owner controls. The new owner
   can administer and delete using the exact group-name confirmation.
8. Try an expired/used/revoked invitation. It must reject joining. Invite metadata
   lists must never reveal previously issued secret codes.
9. Reload/restart plugin and server; resume the same character and check its groups,
   balances and grants. Demonstrate hosted renewal without reopening the browser.
   Sign out one device or revoke a spare device session: it must lose access.
10. Interrupt a currency request by disconnecting the test client. Reconnect and use
    **Retry original action** or **Check committed receipt**. The amount must apply
    once. On an unresolved outcome, retain the original key and reconcile before
    another change. A server restart must not erase the operation record.
11. The chat question still appears once per plugin startup; sends and group/character
    switches must not ask again. M1 diagnostic chat remains a real public message.

Record plugin/source version, hosted .NET/PostgreSQL versions, character/group
setup, observed results and defects. M2 is accepted after these new flows pass.

## Build and automated verification

Keep the [version matrix](../engineering/versions.md) and locked dependencies.
Set DALAMUD_HOME to the supported reference directory when building off Windows.

```sh
dotnet restore AmorRP.sln --locked-mode
dotnet tool restore
dotnet build AmorRP.sln -c Release --no-restore
docker compose -f deploy/compose.yaml --profile test up -d --wait test-database
export AMORRP_TEST_DATABASE='Host=127.0.0.1;Port=55432;Database=amorrp_test;Username=amorrp;Password=amorrp-local-only'
dotnet test AmorRP.sln -c Release --no-build --no-restore
export ConnectionStrings__Database="$AMORRP_TEST_DATABASE"
dotnet ef migrations has-pending-model-changes --project src/AmorRP.Server --configuration Release --no-build
python -m pip install -r scripts/requirements-docs.lock.txt
python scripts/check-docs.py
dotnet build src/AmorRP.Plugin -c Release -p:Platform=x64 --no-restore
python scripts/check-plugin-package.py
dotnet publish src/AmorRP.Server -c Release --no-restore -o artifacts/server
python scripts/package-milestone.py --milestone M2
```

The database variable must identify a disposable test database whose name starts
with amorrp_test. Tests create/drop their own random databases; test credentials
need CREATEDB on that test instance. No production fake-auth option is present.
Test providers are injected only inside test hosts.

Automated evidence on 2026-10-07:

- Seven-project Release build with zero warnings/errors; locked restore passed.
- 62 tests passed, none skipped: 34 plugin, 27 server, one contract boundary.
  Nine M2 checks cover durable login/restart and stable ownership, renewal reuse,
  group/invite concurrency, permission/isolation/ledger behavior, removal/rejoin,
  weekly policy boundary, ownership acceptance and version/pagination/deletion
  recovery. Test provider responses are fixtures, not live XIVAuth evidence.
- EF migration/model consistency, documentation links/OpenAPI (82 operations,
  49 implemented), plugin ZIP identity/dependency/license and source boundaries pass.
- Published native server upgraded a disposable PostgreSQL 18.6 M0 bootstrap
  database to M2; PORT handling, health/readiness, capabilities and protected group
  access were smoke-tested. No real external secret was used.

 These checks do not
claim live game/hosted provider execution. Complete container builds and Windows
CI have separate evidence; the workspace network blocks Microsoft's image CDN.

## Configuration and expansion

`ServicePolicy` validates group caps, weekly ceilings, active-invitation ceiling,
ownership lifetime, public page and request limits. Effective M2 domain ceilings
are advertised in capabilities. `SessionPolicy` configures access/refresh/login
lifetimes and session/attempt caps; defaults are 30 minutes/30 days/10 minutes,
20 device sessions/character and 256 pending logins. Server-wide API/login budgets
are 6000/1200 requests per minute by default; measure/refine at M5 capacity review.
All limits are server-enforced, including clients other than this plugin.

M3 adds group categories, potion definitions/revisions, holdings and per-character
weekly usage through additive migrations and reuses membership authority,
transactions and operation receipts. M4 adds reservations and trade transactions;
M2 stores reserved currency as zero until that path exists. Future multiple
currencies can move balances into a currency-keyed table through a migration;
stable currency IDs and integer-string contracts already exist. No category
name, coin name or new item type is used as a permission decision.

Operator cleanup, retention/purge/export, event streams, complete load/restore
checks and distribution polish remain M5 gates. Do not call this a production 1.0.

## Feedback update, 2026-10-07

The maintainer reports one functional issue: plugin removal fails without visible
history/error. M2 acceptance remains open for that path. Carry its fix and the
requested navigation/refresh/roster/form/image changes into M3, starting with
removal and diagnostics; no M2.1 artifact is planned. See the [feedback plan](m2-feedback-m3-plan.md).
The build/evidence above describes M2 and is not a claim that these fixes shipped.
