# Integration plans

S10 is implemented in the local 0.11 build. S11 remains planned. These adapters depend on server-owned state and accounting;
they do not become alternative identity, currency or ownership authorities.
See [decisions](decisions.md), [economy](economy-and-groups.md) and [work order](README.md).

## S10 — Optional Moodles integration

Amor RP applies configured effects when Moodles is available. The activity log
records IPC readiness, application and removal attempts, errors, and the
status readback. Moodles actions return no success value or rejection reason,
so Amor RP checks the reported status list. The integration respects Moodles
settings and never blocks consumption, billing, release or disbanding.

Configuration is author-side: season administrators define the ownership debuff;
authorized recipe authors define consumable effects. Store enough definition data
for another client to apply the same effect; an author's private saved GUID alone
is not portable. Bound icons/text/duration/stacking values and snapshot definitions
with item revisions. No arbitrary code or external asset execution from recipe data.

Use a small optional Dalamud IPC adapter with readiness/version detection. Apply
only to the locally verified character. The server records the active grant/outcome
and expiry; the client reconciles it with Moodles when possible. Use remaining
duration after reconnect, not a fresh full-duration grant. Store/recover sufficient
managed effect identity to retry cleanup after Moodles returns.

For ownership status, attempt its configured effect while the server says the permit
is active. Check presence before reapplying so routine ticks do not repeatedly stack
or play application effects. On release/disband, attempt removal of that specific
managed effect. Never clear the whole status manager or remove effects owned by
other plugins. Missing visuals are not proof of release and do not unlock billing.

Effect IDs include the season and character. The integration displays only the
active season's effects. Identical saved effects share an ID and refresh one timer.
Potions retain the effect stored on their item revision. Ownership effects use a
short renewable display duration while the permit is active. These are RP status visuals,
not actual FFXIV combat-stat changes. Visibility to other players depends on their
compatible synchronization setup and is not guaranteed by local IPC alone.

### Source evidence and feature-time check

On 2026-10-29 the reviewed published Moodles entry was 1.1.3.5, Dalamud API 15.
The source at `f4b7578c79fcabf42eda3b866736ed35955f3671` exposes IPC version 4,
status definition data, add/update, inspection and individual removal functions.
This is planning evidence, not an executed in-game compatibility test:

- [IPC implementation](https://github.com/kawaii/Moodles/blob/f4b7578c79fcabf42eda3b866736ed35955f3671/Moodles/IPCProcessor.cs)
- [Status definition tuple](https://github.com/kawaii/Moodles/blob/f4b7578c79fcabf42eda3b866736ed35955f3671/Moodles/IPCTypedef.cs)
- [Plugin/application settings](https://github.com/kawaii/Moodles/blob/f4b7578c79fcabf42eda3b866736ed35955f3671/Moodles/Gui/TabSettings.cs)
- [Published feed](https://raw.githubusercontent.com/Ottermandias/SeaOfStars/main/repo.json)

Recheck the installed/published interface and prove add, inspect, remove and remaining-
duration handling with a small in-game adapter test before committing integration
schemas. Do not rely on sticky/unclear persistence flags without testing cleanup.

Verification: missing/disabled/late-starting plugin; unsupported version; rejected IPC;
correct local character; duplicate receipts; expiry; manual removal/reapplication;
release while unavailable; group disbanding; unrelated effects preserved. Confirm
that no user-facing diagnostic or core-action failure occurs in every failure case.

## S11 — Discord writing earnings

The bot is inviteable across Discord servers. Each installation links to an approved
Amor RP group and therefore its season. Proposed first-version association is
one Discord server per group and one group per Discord server; multiple approved
groups give a season multiple connected servers. Treasury approval is independent:
a group may have Discord permission without a treasury.

| Authority | Responsibility |
| --- | --- |
| Season owner | Enables the feature, approves group Discord Bot Permission, sets currency rewards/cooldowns/weekly limits/balance ceiling and season payout budget |
| Group owner | Initiates authorized linking/invitation and disconnects their group |
| Discord server owner/authorized administrator | Approves installation, selects channels and eligible Discord roles/exclusions, pauses activity in that server |
| Player | Explicitly opts in, links their Discord identity and selects one earning character in the season |

Discord's own installation authority cannot be bypassed by an Amor RP invitation
button. Validate both the Amor RP owner and Discord installation association. Public
bot installation URLs cannot prove group authorization: reject/unlink unauthorized
installations, give them no earning access and remove the bot as appropriate.
Linking uses a scoped expiring challenge, not copied Amor RP session/refresh tokens.

### Earning and anti-abuse rules

- No player earns until explicitly opted in and linked. No silent assignment to an alt.
- One earning character per linked Discord account/season. Character or server
  switches retain usage and cannot create additional weekly budgets.
- Reward only configured channels and eligible accounts/roles. Exclude bots, commands,
  edits/replayed messages and trivial repetition. Exact minimum length/cooldown and
  content-intent requirements are feature-time configuration/platform research.
- Apply the weekly personal limit, a season-wide minting budget, and the personal
  balance ceiling before posting an award. Count the full awarded amount even when
  automatic debt collection leaves no wallet increase.
- Proposed ceiling calculation includes owned/reserved personal currency, excludes
  treasury savings and does not subtract debt. Clamp a last award to the headroom.
  Spending restores eligibility. Group treasury approval is an additional owner control;
  no treasury-balance aggregation or perfect multi-account detection is promised.
- Message/award identity must survive retries, reconnect and restart. Duplicate
  delivery never produces a second reward. Define missed-message/backfill policy;
  do not promise perfect recovery from gateway outages or count edits as new activity.
- Use the authoritative credit/receipt/debt-settlement path, not direct wallet updates.
  Discord-specific rules do not grant the bot arbitrary currency-management powers.

Keep identity links private to their operational purpose. Do not publish alt/account
mappings or retain message bodies just for a reward ledger when identifiers and
eligibility results suffice. No AI quality scoring or payment per word is required.

### Lifecycle and hosting

Group disbanding or permission revocation disables the link/earning authorization
immediately in the database. Queue external Discord leave/removal after commit and
retry it durably if the bot/network is offline. Immediate currency authorization
must not depend on successfully contacting Discord. Removing the bot through Discord
also disconnects its earning association. Reconcile after restart.

Render remains testing infrastructure until the private Ubuntu server is ready;
see [hosting and releases](hosting-and-releases.md). Do not provision a second paid
Render service. Validate continuous bot
connectivity, Discord application/intents, token storage and restart behavior on the
chosen host before enabling real rewards. Decide process placement then; a separate
paid machine or a new general integration framework is not a prerequisite.

Verification: wrong group owner, missing approval, wrong Discord server/admin, failed
linking, opt-out, selected-character changes, multi-server limits, ceiling crossings,
reserved balances, concurrent rewards, creditor collection, duplicate gateway events,
restarts, group deletion and delayed bot ejection. Use test Discord installations
and disposable currency data; no live rollout is implied by these documents.
