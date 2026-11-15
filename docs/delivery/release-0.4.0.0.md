# Amor RP 0.4.0.0 — seasons and server administration

This coordinated server/plugin release makes the former Community a **Season**.
RP groups remain inside their season. Only a server administrator may create or
activate seasons. Verified characters automatically join the single active season;
players no longer create communities, enter invitation codes or leave seasons.

The 0.4.0.0 plugin also supports the existing 0.3.0.0 server. Until the server
advertises `seasonsEnabled: true`, it retains legacy community behavior and does
not request the new season endpoints. Plugin installation can precede the server
upgrade; the seasonal behavior above requires the configured 0.4.0.0 server.

## Administration and identity

Set `SeasonPolicy__Enabled=true` and
`SeasonPolicy__AdministratorLodestoneIds__0` to the numeric Lodestone ID of the
administrator character. The home stack maps private Portainer variable
`AMORRP_ADMIN_LODESTONE_ID` to that setting. Use the number from the public
Lodestone profile URL, not the character name or the plugin's internal UUID.
Additional administrators can be configured with array indexes 1, 2 and so on.

An authenticated session must resolve to that character's **provider-verified**
Lodestone ID. Client headers, names, supplied IDs and being the first person to log
in never grant administration. An empty allowlist grants nobody authority. Existing
ownership bindings and XIVAuth login/pairing remain unchanged. This is a character
allowlist; it does not grant the character's alternate characters administrator access.
The real administrator ID stays in private deployment settings, not this repository.

Server administration controls season creation/lifecycle. Existing season owners and
delegates retain their scoped controls. It does not bypass private RP group authority.

## Season lifecycle and data

- **Draft:** preparing a season creates a new economy with its own currency, seed
  categories and zero balances. The initial weekly policy is chosen in the creation
  form. Drafts are read-only until activation.
- **Active:** activation enrolls all existing verified characters. New characters
  are enrolled on authenticated season discovery/listing, without an invitation or
  the former joined-community quota. Enrollment is idempotent across replicas.
- **Archived:** activation of another season archives the current one atomically.
  Only one season can be active (database unique index and shared lifecycle lock).
  An administrator may also archive the active season without opening another.
  An archived season cannot be reopened.

Activation/archive requires a fresh season version, an operation key and the exact
season name typed into the confirmation form. Replaying the same completed command
recovers its receipt. Concurrent lifecycle requests serialize against all affected
community economy locks.

Each season keeps separate membership, groups, wallets, treasuries, items, writing,
definitions, allowances, bills/debts, trades and audit records. No balance, permission
grant or asset is copied into a new season. Open trades are cancelled, reservations
released and released funds settled under the previous rules before archival.
Background debt collection then skips that archive. Existing held funds/debts stay
recorded there; the new season does not inherit them. Historical members can browse
their authorized records and owned writing. Newcomers do not receive archive access.

Fresh mutations to archived/inactive seasons fail server-side in the shared command
commit gate. Successful old receipts remain recoverable without reapplying changes.
Reading announcements can still update personal read markers, and local private
notes can be edited. Archived group events leave the automatic upcoming-event feed.

Enrollment never reactivates a removed/blocked membership. The season owner uses
**Members → include inactive → Restore season membership**, with a reason, to restore
access explicitly. Previous grants and group authority are not restored automatically.

## Existing installation upgrade

The additive `Seasons` migration labels existing communities `legacy`; it never
deletes data, chooses an active community or resets the economy. In seasonal mode,
legacy communities are read-only until an administrator activates one. Activating
an existing community preserves its original currency, groups, balances and records
while enrolling verified players. This is the recommended first transition for the
current home-server installation. The following newly created season starts fresh.

1. Back up the existing database and retain the original vault key and password.
2. Run the existing home updater to build the verified main server source. In
   Portainer, use the updated [home stack](../../deploy/home/compose.yaml), retain
   existing private values/volumes, set the generated image tag, and add
   `AMORRP_ADMIN_LODESTONE_ID` with the administrator's numeric Lodestone ID.
3. Update the stack with image re-pull disabled. Check `/health/ready`, then
   `/api/v2/capabilities`: server/minimum client **0.4.0.0**, API **2**,
   `seasonsEnabled: true`.
4. Install/update the plugin to 0.4.0.0 from the stable feed. Plugin publication uses
   the exact Windows artifact from successful matching CI; it does not require the
   home server to have been upgraded first. Once server readiness and seasonal
   configuration are confirmed, sign in as the configured character, open the
   management cog → **Seasons**, and activate the existing community by name.
5. A second verified character should join automatically. Confirm its roster,
   inventory, crafting, writing and trade access. Prepare and activate a fresh test
   season only when ready to archive the existing one.

Generic deployments retain legacy community behavior unless `SeasonPolicy.Enabled`
is explicitly enabled. Archived rows remain read-only even if the rollout switch is
later disabled. Disabling the switch is not a schema rollback. Do not run an older
server against a newer database; restore a coordinated backup if rollback is needed.

### Server 0.4.0.0 still allows ordinary community creation

The server version alone does not enable seasons. Portainer's stack-variable list
supplies Compose substitution values; entries do not automatically become API
container environment variables. In the stack editor, retain the existing settings
and ensure these lines are inside the existing `services.api.environment` mapping:

```yaml
SeasonPolicy__Enabled: "true"
SeasonPolicy__AdministratorLodestoneIds__0: ${AMORRP_ADMIN_LODESTONE_ID:?Enter the administrator Lodestone character ID}
```

Set `AMORRP_ADMIN_LODESTONE_ID` in Portainer's variables list, then **Update the
stack** so the API container is recreated with the settings. Restarting an existing
container does not apply changed environment variables. In the running API
container's environment, confirm only the two `SeasonPolicy__...` values; do not
copy or share the full environment, which also contains credentials.

After reconnecting, Settings → About should say **Seasons enabled**. The public
`/api/v2/capabilities` response must contain `"seasonsEnabled": true`; a false value
means ordinary community creation remains enabled by legacy mode. If it is true
and an unlisted character can still create a season, investigate the authenticated
character's verified Lodestone identity and the effective administrator allowlist.
Signing into the administrator character is needed for season lifecycle actions,
not for enabling this server configuration.

## Plugin behavior and game checks

The launcher retains its writing, crafting, season browsing, group management,
season management, trade and closed-eye controls. The browsing window retains
Overview/Members/Groups/Noticeboard tabs. The administrator sees season lifecycle
controls even before the first active season exists. Ordinary players do not see
season creation or lifecycle actions. Existing scoped management permissions remain.

Login selects the active season. A newly activated season becomes selected on the
next discovery refresh; explicitly browsing an archive survives routine refreshes.
Inventory and Writing log show archive state and disable mutations while preserving
reading/copying. Draft/archived views never redirect actions into another economy.
The vanilla inventory attachment and eye switch are unchanged.

In-game acceptance still needs the two verified characters, administrator-only cog
controls, first activation, automatic enrollment, scope changes across all open
windows, archive reading, a fresh season's empty economy, and an interrupted command
recovery. Automated checks cover authorization, empty allowlists, concurrent
activation, version conflicts, retry receipts, enrollment/moderation, historical
quotas, asset separation, retained private writing/groups and released reservations.
No taxes, commissions, gathering or external bot work is included.
