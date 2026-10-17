# Amor RP 0.0.6.17 — beta release on main

Source branch: `main`. This candidate was promoted from `testing` to the normal
channel for the closed beta. A separate testing backend is deferred; the existing
Render service and database are used during this beta phase. Feature work on the
`testing` branch is retained, but no separate testing candidate is advertised.
The installation URL stays https://raw.githubusercontent.com/Runmih/AmorRP/main/repo.json.
The normal install and update links now offer 0.0.6.17; no Dalamud testing opt-in
is required. Promotion reuses the exact verified Windows package and preserves
its original build provenance.

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

Deploy `main` to the existing Render service before using 0.0.6.17. Keep the
same database, public address, vault keys, XIVAuth client and callback settings.
Retain the existing startup migration arrangement. No data wipe is required.

Migration 20261017002828_CommunitiesAndGroups renames the existing groups table
to communities, GroupId columns to CommunityId, and their indexes/constraints.
It preserves IDs, balances, inventories, quotas, announcements, logos, memberships,
sessions and encrypted receipts, and converts General Moderation's stored identifier.
The new groups table contains child profiles with community-scoped foreign keys.

Stable clients retain the API v1 routes and JSON/ETag shapes through an explicit
adapter. Old encrypted pending actions keep their original request URL and fingerprint;
the 0.0.6.17 plugin converts their results and UI callbacks to current terminology.
Saved community selections keep the previous configuration key. Private notes stay
local, encrypted and associated with the same IDs.

The previous 0.0.6.16 plugin remains supported against this server. Database
downgrade is refused if any child groups have been created, to prevent losing profiles.
Do not run an old server against the renamed schema.
