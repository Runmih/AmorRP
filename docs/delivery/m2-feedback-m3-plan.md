# M2 feedback and M3 implementation order

Recorded 2026-10-07 from the maintainer's in-game feedback. M2 has a reported
member-removal defect; do not label it fully accepted. Most tested flows were
reported usable. No M2.1 artifact is scheduled: carry the fixes into one M3 build,
with removal verified before M3 acceptance.

Implementation update: M3 code now includes these changes; see the
[M3 guide](m3-install-test.md). The original investigation/ordering below records
the reasoning; live acceptance is still pending.

## Feedback disposition

| Feedback | Planned behavior | Milestone |
| --- | --- | --- |
| Group selector outside tabs | One persistent dropdown above the tab bar, visible from every tab, listing active memberships only; save selection per backend/verified character and clear old-group private views on switch | M3 |
| Refresh and stale data | Keep a global Refresh data button beside the selector; show last successful update and stale/offline state; refresh after committed changes. Add a 60-second background read refresh while an authenticated product window is visible, skipping pending commands and preserving local drafts/selection | M3 baseline; verify load/reconnect behavior by M5 |
| Remove/block does nothing and no history appears | Fix removal interaction first; dedicated mandatory reason, clear confirmation and validation, persistent rejection/result feedback; prove server removal, access loss and one committed history event through the actual plugin | First M3 fix and acceptance blocker |
| Image currency symbol | Replace the text-symbol editor with owner upload/preview for static PNG, JPEG/JPG or WebP, both dimensions at most 128 pixels. Persist via the backend and render for members; bundled default icon when absent | M3; required by 1.0 |
| Left character remains dormant in owner roster | Retain dormant membership/assets internally. Default to active roster for everyone, including owner; add owner-only Show inactive members for dormant/blocked recovery. Ordinary members never see inactive rows | M3 |
| Join code stays after success | Clear the entered join code and reset visibility acceptance after confirmed success, including a confirmed same-key replay. Retain entered data after rejection or unresolved outcome | M3 |
| Group-creation form stays after success | Reset only the creation draft to current server defaults after confirmed creation. Keep owner edit drafts separate; retain draft on failure/unknown outcome. Select the new group | M3 |

The existing M2 plugin already has a global **Refresh groups** button and reloads
its views after successful commands. The feedback is about discoverability,
background freshness and preserving visible command feedback across these reloads.
The new label describes all refreshed data, not only the group list.

## Removal investigation

Source inspection identified two concrete problems in the current UI:

- Selecting a member resets the shared `reason` string to empty. Removal submits
  that field, whose input currently sits beside the currency adjustment controls.
  The server requires a nonempty removal reason and returns 422 `invalid_request`
  for an empty/whitespace value. A stale member ETag can separately yield 412.
- `GroupsPanel.Collect` sets the rejection message, then schedules a refresh for
  most 4xx command failures. `Refresh` immediately changes status to Loading groups
  and ultimately Groups refreshed. The failure therefore disappears from view.

This explains a plausible observed failure, but the live request/response from
this report was not captured. Do not claim the exact rejection is confirmed.
The existing PostgreSQL removal/restore/rejoin scenario passes when a valid reason
and current member ETag are supplied; this does not prove the plugin interaction.

Implement a dedicated removal reason and confirmation, not a shared currency draft.
Disable the removal action until the required reason is valid. Keep an independent
last-action result/error banner across automatic/manual read refreshes. Show useful
user wording and expandable sanitized HTTP status, ProblemDetails code, request ID
and operation key; never show tokens, invitation codes, private content or raw
response/request bodies. Version rejection requires review after refreshing, with
no automatic reissue under a new key. Retain unresolved command keys for recovery.

Only a committed removal records `member.remove` in authoritative group history.
Validation/authorization failures must not create a successful kick event. Show
failed attempts as client diagnostic outcomes; do not conflate them with history.

## Currency image implementation boundary

Currency images are a new 1.0 feature, replacing the earlier deferral of all uploads.
This adds server media handling, persistence and plugin texture lifecycle work;
it is not just a different text widget. Keep the implementation narrowly scoped.

- Owner uploads a local static PNG, JPEG/JPG or WebP file; verify actual decoded
  format, not the filename/MIME alone. Reject animation, malformed files and either
  dimension over 128. Do not silently crop or fetch a user-supplied URL.
- Recommended additional service ceiling: 256 KiB encoded upload, advertised to
  clients and enforced before decoding. Bound decoder memory/pixel/frame work.
  Configure a separate bounded multipart/request allowance for the icon route;
  existing 64 KiB JSON limits must not accidentally reject a valid icon upload.
- Normalize accepted images to static PNG and strip metadata. Use a maintained,
  deployment-compatible decoder with appropriate distribution licensing; verify
  Linux Render and Windows plugin compatibility during M3 implementation.
- Store the small normalized binary in PostgreSQL with an opaque asset ID, group
  scope and content hash. Render's ephemeral filesystem is not durable storage.
  Currency references the asset; replacing/removing an icon never changes its
  currency ID, balances or ledger. Add a migration and retain M2 data.
- Read requires current active group membership; upload/removal requires current
  ownership, version precondition, idempotency and an audit receipt. Deliver only
  from the configured backend; never put bearer credentials in image URLs.
- Implemented routes in OpenAPI: GET/PUT/DELETE
  `/api/v1/groups/{groupId}/currency/icon`. GET returns normalized `image/png`;
  mutations return the updated currency and operation receipt. No general image
  hosting, item images, letter attachments or public media directory is included.
- Use a bundled default coin icon until configured. Textual `symbol` and
  `currencySymbol` fields remain legacy M2 compatibility data during migration;
  the release UI offers image selection. Add an optional icon asset reference to
  currency responses without reinterpreting stable currency identifiers.
- Load asynchronously, bound the texture cache, release textures on replacement,
  access loss and unload. Scope caches to backend/character/group/asset version.
  Asset infrastructure may be reused for future item icons without accepting them
  as part of this milestone.

## M3 work order and exit evidence

1. Resolve removal and persistent error feedback. Exercise a blank reason and a
   successful reasoned kick in the plugin; capture safe rejection details if the
   server still refuses. Confirm removed member loses group access and the owner
   sees one history entry. Cover stale-version rejection and same-key recovery.
2. Implement shared group dropdown, active-only roster/inactive-owner view, global
   refresh, 60-second read refresh and success-only form resets. Avoid overwriting
   policy/edit/letter drafts, resetting chat consent or changing a pending action's
   group/character when read data updates. Do not reload the whole plugin to sync.
3. Implement currency icon upload/storage/rendering and its bounded API. Check real
   valid formats, oversize/malformed/animated rejection, owner/member isolation,
   replace/remove/restart and safe texture disposal. No tests that merely mirror UI.
4. Build the planned M3 inventory: categories, owner-defined immutable potion
   revisions, creation using independent weekly points, letters/quotas/eligible
   edits, extended/compact inventory and deliberate consented potion use.
5. Ship one M3 artifact and upgrade guide. Acceptance includes all carried M2 fixes,
   a successful owner-plus-two-members inventory/use session, restart persistence,
   meaningful accounting/idempotency checks and the shared-display requirements.

A separate M2.1 build becomes necessary only if investigation finds asset loss,
duplication, unauthorized access or another defect that makes continued testing
unsafe. The reported removal/UI failure currently does not establish such a defect.
Final visual polish remains M5; functional navigation and understandable failures
are addressed during M3.
