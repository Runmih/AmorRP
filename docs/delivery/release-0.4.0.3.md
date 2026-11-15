# Amor RP 0.4.0.3 — All players share all seasons

Every verified character is automatically included in every non-deleted season:
existing and new seasons, drafts, the active season and read-only archives.
A player never joins or leaves a season. Bans belong to separate user moderation,
not season membership. RP groups inside seasons retain their own membership,
permissions and private content.

The plugin removes remaining join/create prompts and all season membership
removal, block, restore and inactive-member controls. Ownership transfer and
scoped gameplay permissions remain available to authorized staff. Creating
seasons still requires `seasons.create`; configured Lodestone administrators
receive it automatically. Activation and archival remain administrator actions.
Lifecycle and global creation-permission commands now work while a draft or
archive is selected; ordinary gameplay writes remain blocked there.

## Existing data and automatic inclusion

The `SharedSeasonMemberships` migration restores old dormant/blocked season
memberships to active without restoring their old grants. It preserves character
and season IDs, balances, holdings, descriptions, quotas and read markers.
It fills missing character/season memberships with zero balances. Repeated
refreshes and sign-ins preserve existing financial records and grants.

New verified characters receive every existing season during sign-in. New drafts
include every existing character immediately. Discovery and direct season reads
also fill missing memberships safely, covering concurrent sign-in and creation.
Each season keeps its own economy; automatic inclusion does not copy assets,
private writing, scoped permissions, RP-group membership or money between seasons.
Private history, operation receipts, group content and owner audit retain their
existing authorization rules.

The old season join, leave, member removal/block/restoration routes are absent.
Database membership status now permits only active. There is one seasonal model,
no mode switch and no older-server fallback.

## Updating and checking

Update both plugin and home-server image to **0.4.0.3**. Use the existing home
updater and Portainer process, retaining the existing database, vault key and
private credentials. Keep the Portainer variable `AMORRP_ADMIN_LODESTONE_ID`.
The stack passes it to the API with its existing environment mapping; no new
configuration variable is needed.

Manual in-game checks:

- Sign into an ordinary character: every existing season appears, with no season
  join/leave controls or creation button. Drafts and archives are read-only.
- Sign into the configured administrator: create a draft, then activate it while
  that draft is selected. Another character receives the draft automatically.
- Sign into a new character after a rollover: both current and archived seasons
  appear; each balance starts at zero and other players' private records stay private.
- Reopen/refresh repeatedly: balances, inventory, quota use and permissions persist.
- Check the member-management screen: permission and ownership controls remain;
  season removal/block/restoration controls are gone.

Automated verification covers all-season inclusion, concurrent refreshes, migration
from former dormant/blocked rows, absent membership commands, personal record
privacy, lifecycle submission while browsing inactive seasons and existing feature
regressions. Publish the exact Windows artifact after matching server and Windows
CI succeed; automated checks do not replace the manual in-game checks above.

## Published package

The stable feed publishes **0.4.0.3** from source
`b366dba53992d587d0d7f5ea7689e11f8b3c2458`, using the exact
[successful Windows artifact](https://github.com/Runmih/AmorRP/actions/runs/43760674724).
The matching [server/contracts workflow](https://github.com/Runmih/AmorRP/actions/runs/43760674739)
passed **190 server integration, 181 plugin helper and 1 contract tests**, migration
model consistency, documentation, deployment/startup checks and the server image build.
The artifact archive matches GitHub's digest. The immutable inner package SHA-256
is `6046304196e2a5d3b8a66c429b6294ad8204e2f587a21429fbee435536ff0108`.
The package manifest reports 0.4.0.3 and Dalamud API 15.

The home server requires its manual Portainer image update; automated checks do
not substitute for the in-game checks listed above.
