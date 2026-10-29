# Amor RP 0.1.0.0

## Changes

- Community permissions are organized into Gameplay, Moderation, Recipe creation,
  Group administration and Economy. Membership actions are separate.
- Community owners can enable group announcements, noticeboard posts and events
  in Groups → Features. Existing and new groups start with all three enabled.
- Group owners can assign those enabled tasks to group members in Groups → Staff.
  Staff can publish announcements, maintain the group's notice and manage events
  according to their individual grants. Profile, logo, joining rules, staff grants
  and deletion remain under their existing owner/administrative authority.
- Unauthorized editing controls/tabs are hidden. Community Manage Groups still
  grants deletion only and does not expose private rosters/announcements.
- Removing grants or disabling a feature takes effect on the server immediately.
  Refresh groups retrieves the current controls. Leaving a group/community clears
  staff grants; rejoining does not restore them.

## Deployment

Redeploy the latest main server on Render before testing the updated plugin.
Normal configured migration startup/pre-deploy applies
`20261029200422_GroupStaffPermissions`. It adds group feature arrays and group
membership grants with fresh permission revision IDs. It preserves existing profiles,
memberships, balances and owner features; it does not grant staff authority to members.
No data wipe or private Ubuntu migration is part of this release.

Plugin and server are both 0.1.0.0; the server advertises minimum client 0.1.0.0 and
the plugin requires server 0.1.0.0. Update from the existing main Dalamud feed.
Amor RP API remains v2, XIVAuth remains external v1, Dalamud API remains 15.
Provider login code, endpoints, scopes and callback configuration are unchanged.

This is S01's permission foundation. Treasury, billing, Discord, classes and Moodles
are not implemented by this release and have no placeholder controls.

## Verification

Local automated verification passes 313 tests: 171 PostgreSQL/server tests, 141 plugin
helper tests and one dependency-boundary test. Plugin/server builds have no warnings
or errors; documentation, startup, schema consistency and package checks pass.

Automated checks cover migration preservation, default owner access, delegated
content, feature/staff revocation, group scope, privilege escalation, membership
removal/rejoin, version preconditions and replay. Production draft components are
tested for preserving edits and keeping group/member targets separate. Existing
community permissions, authentication, inventory and trading regressions remain in
the full suite. CI validates schema consistency, server build/container and the
Windows plugin package before distribution.

In-game rendering and interaction need the maintainer's checks below; no game client
is available in the build environment. S01 acceptance remains pending that feedback.

## Major in-game checks

1. As community owner, review Members → Community permissions and Groups → Features;
   save a change and confirm it persists after refresh.
2. As group owner, grant a joined member announcements, notice and events in Staff.
   On that member, Refresh groups and publish one of each successfully.
3. Remove a staff grant or disable its group feature. After Refresh groups, the member
   loses its editing controls and cannot perform that action.
4. As a community owner/moderator outside the group, confirm private announcements,
   roster and staff management stay inaccessible; Manage Groups still permits deletion.
