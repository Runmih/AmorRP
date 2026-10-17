# Amor RP 0.0.6.17 — testing

Source branch: `testing`. Stable source/plugin remains `main`, version 0.0.6.16.
The installation URL stays https://raw.githubusercontent.com/Runmih/AmorRP/main/repo.json.
The feed advertises 0.0.6.16 through AssemblyVersion and stable download links,
and 0.0.6.17 through TestingAssemblyVersion, TestingDalamudApiLevel and DownloadLinkTesting.
Enable Dalamud testing and Receive plugin testing versions for Amor RP to opt in.

## Changes

Existing groups become communities throughout the current UI, domain code, API
v2 and database. Each community has a Groups tab for child group profiles, with
a name, description (300 characters) and independent clickable logo. These groups
can represent any organisation; no venue-specific types are imposed.

The community owner has Create Groups and Manage Groups automatically and can
grant them independently through Members → Manage membership → Moderator permissions.
Create permits new profiles; Manage permits editing/deleting profiles and changing
logos throughout that community. These grants do not confer other moderator rights.
All active community members can view profiles. Group memberships and separate
group economies are not introduced by this profile feature.

## Server upgrade and compatibility

Deploy `testing` to the existing Render service before testing 0.0.6.17. Keep the
same database, public address, vault keys, XIVAuth client and callback settings.
Retain the existing startup migration arrangement. No data wipe is required.

Migration 20261017002828_CommunitiesAndGroups renames the existing groups table
to communities, GroupId columns to CommunityId, and their indexes/constraints.
It preserves IDs, balances, inventories, quotas, announcements, logos, memberships,
sessions and encrypted receipts, and converts General Moderation's stored identifier.
The new groups table contains child profiles with community-scoped foreign keys.

Stable clients retain the API v1 routes and JSON/ETag shapes through an explicit
adapter. Old encrypted pending actions keep their original request URL and fingerprint;
the testing plugin converts their results and UI callbacks to current terminology.
Saved community selections keep the previous configuration key. Private notes stay
local, encrypted and associated with the same IDs.

Downgrading only the plugin to stable is supported against this server. Database
downgrade is refused if any child groups have been created, to prevent losing profiles.
Do not run an old server against the renamed schema.
