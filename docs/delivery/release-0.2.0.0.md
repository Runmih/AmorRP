# Amor RP 0.2.0.0

## Changes

- Group currency treasuries, individually enabled by the community owner in Groups
  → Features → Currency treasury. Existing and new groups start without approval.
- Joined members can deposit their own available coins. Group owners can withdraw
  coins to their own character and separately delegate withdrawal and record access
  in Groups → Staff. Unavailable controls stay hidden.
- Groups → Treasury shows the group balance, your available coins, optional transfer
  notes and receipts. Authorized staff can browse the group's financial records.
- Disbanding returns treasury currency to the actual group owner, including retained
  inactive/blocked memberships. Community deletion settles all groups under the same
  rule. The deleting moderator never becomes the beneficiary.
- Any return that would overflow the owner's balance remains on the retained deleted
  group and can be collected through Groups → Treasury returns into the original
  community account. This list remains available without a selected community.
- Retry responses keep the original treasury receipt and group/community identifiers.
  The pre-v2 receipt converter now recognizes already-current responses correctly.

No billing, debt, tax, commission or group item storage is included in this slice.
S01 (0.1.0.0) passed the maintainer's major in-game checks on 2026-10-29.

## Deployment

Redeploy latest main on Render before updating/testing the plugin. Normal migration
startup/pre-deploy applies `20261029204527_GroupTreasury`. It adds zeroed group
accounts and immutable financial records without changing existing character balances,
reservations, memberships, group profiles or feature approvals. There is no data wipe.
Downgrade refuses to discard financial balances or receipts.

Plugin and server are both 0.2.0.0; minimum client/server is 0.2.0.0. Use the existing
main Dalamud feed. Amor RP API remains v2, XIVAuth's external API v1 and Dalamud API15.
Login/provider source, callback configuration and scopes are unchanged. Hosting remains
Render; no Ubuntu cutover is included.

## Verification

Local verification passes 323 tests: 180 PostgreSQL/server integration tests,
142 plugin helper tests and one dependency-boundary test. The nine treasury tests
also pass against the final schema mapping. Plugin/server builds have zero warnings
and errors; documentation, model consistency, startup and package checks pass.

Automated verification uses real disposable PostgreSQL databases for approval,
separate staff grants, private records, feature revocation, available versus reserved
funds, conservation, concurrent withdrawals/deletion, response replay, restart recovery,
moderator deletion with an inactive owner, multiple-owner community deletion, overflow
recovery, scoped pagination and migration preservation/downgrade protection.

Linked plugin transport tests cover typed treasury command routing and group scope.
The full server, plugin helper and dependency-boundary suites must pass, alongside
schema, documentation, package and startup checks, before main feed publication.
Windows CI supplies the distributed package. In-game acceptance remains pending.

## Major in-game checks

1. As community owner, enable Currency treasury for a group. Its owner/member should
   see Treasury after Refresh groups; an unapproved group should not show that tab.
2. Deposit coins, then withdraw some. Confirm the character/group balances and receipt
   show the matching amounts after refresh.
3. Give a member withdrawal and record permissions in Staff. Confirm they can use those
   controls; remove the permissions and confirm the controls disappear after refresh.
4. Disband a funded test group. Confirm its remaining coins go to that group's owner,
   including when the community owner deletes someone else's group.
