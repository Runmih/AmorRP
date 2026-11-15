# Amor RP 0.3.0.5 — crafting and community workspaces

This plugin-only update adds four independently movable/resizable windows:

| Window | Contents |
| --- | --- |
| Crafting | Alchemy and Cooking production, searchable enabled recipes, quantity/cost/weekly allowance and authorized recipe editing |
| Group management | Groups the character owns or has enabled staff actions for; owner profile/logo, announcements, events, notice, billing, treasury and staff controls according to existing grants |
| Community management | Relevant delegated community controls; owner-only features, policies, invitations, economy and lifecycle settings |
| Community | Overview/announcements, Members, Groups and Noticeboard tabs for browsing and personal actions |

The maintainer chose the combined Community browsing window. Management forms are
kept out of its profile/member/notice views. It retains self-description/private
notes, trade entry, group joining/leaving/favorites/trust, personal treasury deposits,
personal bills, ownership acceptance and treasury returns. Manage this member/group
opens the relevant management workspace; Noticeboard → View group selects the Groups
tab rather than redirecting to the old main menu.

## Launcher and access

The icon order is writing book, crafting hammer, community people, authorized group
management tree, authorized community management cog, trading arrows and closed eye.
Management icons and tabs are hidden without relevant authority. The community icon
shows unread announcements; the writing icon remains dedicated to writing. No generic
replacement main-menu button is added.

`/amorrp crafting`, `/amorrp groups`, `/amorrp manage` and `/amorrp community` open
the respective windows. Existing writing/trade/panels commands remain. These are
manually opened windows; hiding automatic panels does not close them or discard work.

Community owners do not inherit private group membership or staff powers. Community
group-feature approval and community moderator deletion remain separate from private
group management. A group's enabled feature and staff grant are both needed. Normal
members retain personal treasury deposits in the browsing view; withdrawals and
treasury records are shown in group management according to their existing grants.
General Moderation remains profile/currency naming, not membership/grant authority.
Server-side checks are unchanged and remain authoritative.

## State and verification

Windows share the selected community/member/group and the existing session, protected
commands, drafts and refresh coordinator. Changing selected members/groups asks before
discarding unfinished edits/notes. Background refresh preserves existing drafts and
save preconditions. Shared file/announcement dialogs are drawn once per frame even
with multiple workspaces open. Scope resets clear view state and pending selections.

Crafting filters the complete loaded definition set by discipline/search and excludes
disabled/retired recipes. Selection follows stable recipe ID across reordering and
refresh, using the returned revision/cost. Writing stays in its separate log.

All 187 plugin/helper tests pass. New cases cover delegated/menu authority, group
feature gating, scope/membership, community-owner/private-group separation and recipe
selection under filtering/refresh. The API 15 build, package and documentation checks
are required, together with successful Windows and server/contracts CI for the exact
published source. Immutable package provenance records the Windows artifact.

Server/minimum client remain 0.3.0.0, API v2, Dalamud API 15. There is no new backend
feature, database migration or dependency. S04 taxes/commissions and gathering remain
planned; this interface change does not mark billing acceptance completed.

## In-game checks

1. Open all four windows through the launcher. Check rescaling, scrolling, narrow
   layouts and separate positions; the launcher keeps the closed-eye icon without text.
2. As a normal member, check Community browsing, self-description, private notes,
   trade entry, group membership/favorites/trust, personal deposits and notice links.
   Management forms/icons must not appear without the relevant permission.
3. As community owner/delegate, exercise only the granted management tabs. A community
   owner outside another owner's private group must not see its private members,
   announcements, billing or treasury records merely through community authority.
4. As group owner/staff, check the filtered group list, profile/staff/notice/event
   forms and authorized finance tabs. Revoke a feature or grant and confirm the UI
   hides the affected controls and the server still rejects unauthorized commands.
5. Start a member/group draft, browse through another open window, and verify Keep
   editing preserves it and Discard changes switches to the correct stable identity.
   Test community/character changes and delayed responses for stale private content.
6. Search/craft Alchemy and Cooking, change recipes/quantities, refresh and exhaust
   allowance. Verify displayed recipe/revision/cost and received items match. Check
   recipe-author controls remain separate from crafting grants.
7. Open file/announcement dialogs with multiple windows open and confirm they render
   once. Test ordinary login, writing, trading, saved-action recovery and panel hiding.
