# Amor RP 0.0.6.19

## Player changes

- Background refreshes now preserve unfinished group joining-mode and password changes, as well as name and description edits.
- If someone else changes the group while you are editing, your unfinished values remain visible and the existing reload prompt protects against overwriting their changes.

## Deployment

Update the plugin through the existing main Dalamud feed. Server 0.0.6.18 remains supported; this patch needs no server redeployment, database migration or XIVAuth configuration changes.

## Verification

The group editor uses a small draft component for its editable values, saved baseline and save precondition. The refresh and save button use the same dirty-state check.

Fourteen regression cases cover every editable field, repeated refreshes, remote conflicts, clean refreshes, reverting edits, explicit reload/save acknowledgement, group/context changes and logo updates. The full helper suite has 104 passing cases; the plugin build and package checks also pass. Final rendering in Dalamud remains a manual check.
