# Amor RP 0.4.0.2 — Seasons only

Historical release. [0.4.0.3](release-0.4.0.3.md) replaces active-season-only enrollment
and season-scoped membership exclusion with shared access to all seasons.

Seasons replace Communities outright. This release removes the mode switch,
older-server fallback, invitation/join flows and old community/API aliases.
There is one seasonal model with RP groups inside it, one active season at a time,
automatic verified-character membership and separate economies for each season.

## Creation permission

Creating a season requires the global **`seasons.create`** permission. Configured
Lodestone administrators receive it automatically through their verified character
identity. The maintainer's existing administrator ID setting therefore grants the
permission by default. Knowing a public character ID or having season/group staff
permissions does not grant it.

An administrator can grant or revoke creation permission for another verified
character under **Season management → Creation access**, with a required reason.
Creation prepares a draft. Activation, archival and global permission delegation
remain administrator actions. Removing a character's creation grant takes effect
server-side immediately; the next context refresh removes its creation controls.
The administrator's default grant is protected from revocation in that screen.

Every creation entry point checks the same permission. Ordinary players do not see
creation buttons, creation windows or the Seasons administration tab. Existing
season owners/staff retain only their scoped management controls. There are no
Create community, Join community, invitation or voluntary season-leaving controls.
The server also denies unauthorized direct creation requests.

## Data and current API

The current API uses `/api/v2/seasons`, `seasonId` and `seasonName`. Only the
current API is exposed; the external XIVAuth provider continues using its own API
v1. No old community or application API v1 routes are mapped.

The migration converts existing unconverted community records to **season drafts**,
preserving their IDs, owners, currencies, balances, inventory, writing, groups and
records. Existing active/archived seasons keep their state. Historical storage
table/column names stay mapped to the same data; they are not a second runtime
model. Existing moderation grants are migrated to `season.moderate`.

An administrator activates the desired existing draft to continue its economy.
Activating a newly created draft starts a fresh economy. The former active season
becomes a read-only archive. Open trades are cancelled and their reservations
released before archival; debt collection skips inactive seasons. Historical
membership remains required for archived/private records. Automatic enrollment
never reactivates removed or blocked membership.

## Home-server update

Use matching plugin/server **0.4.0.2**. Both enforce that baseline and do not fall
back to older community behavior.

1. Build the verified main server with the existing home updater.
2. In Portainer, use the updated [home stack](../../deploy/home/compose.yaml), set
   its newly built `AMORRP_SERVER_IMAGE` tag, and retain the existing database,
   credentials, vault key and `AMORRP_ADMIN_LODESTONE_ID` setting. Remove
   `SeasonPolicy__Enabled` if present. It is not read or required.
3. Update the stack so the API container runs the new image and migration. The
   administrator ID is the only season-administration setting needed by this stack.
4. Install/update the plugin from the normal stable feed. Sign into the configured
   administrator character and activate the chosen draft from Season management.

The previous enable flag can neither disable the seasonal model nor restore
ordinary creation. An unconfigured administrator list fails closed: nobody gains
creation permission by being the first player to sign in.

## Verification

Checks cover default administrator creation, explicit grant/revocation, denied
self-grants, hidden creation controls without the permission, absent old routes,
automatic membership, migration preservation, archive isolation, idempotency,
concurrent activation and the existing inventory/trade/billing/group/auth flows.
The exact Windows package is published only after matching Windows and server CI
pass. In-game checks still cover the final layouts, both character roles, grant
refresh, creating/activating a draft and reading the previous season's archive.

This supersedes earlier compatibility guidance. Do not add a mode, fallback or old
flow without an explicit maintainer request; see [AGENTS.md](../../AGENTS.md).

## Published package

The stable feed publishes **0.4.0.2** from source
`5adf0240bc005dfc687511586c7d1db3f5a5809e`, using the exact
[successful Windows artifact](https://github.com/Runmih/AmorRP/actions/runs/43751981669).
The matching [server/contracts workflow](https://github.com/Runmih/AmorRP/actions/runs/43751981687)
passed all **190 server integration, 179 plugin helper and 1 contract tests**,
model consistency, documentation, deployment/startup checks and the server image build.
The downloaded artifact archive matches GitHub's digest. The immutable inner
package SHA-256 is `bf96f1b5035586391be2106c09ce29ec8dce4317f172cf64c68b63426f5e0153`.
The package manifest reports 0.4.0.2 and Dalamud API 15.

The home server still requires its manual Portainer image update. Automated checks
do not substitute for the in-game acceptance steps above.
