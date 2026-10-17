# M3.1 permissions and potion creation fix

Plugin **0.0.4.2**, Dalamud API **15**, existing M3 server **0.0.4** and API **v1**.
Use branch `m3-inventory`. This is a plugin-side corrective/diagnostic patch,
with permission interaction confirmed by the maintainer after 0.0.4.1; actual
potion creation after the 0.0.4.2 button correction still needs live verification.
No server API, database schema, permission policy or migration changes are required.
Do not redeploy or replace your existing server/database just to install this ZIP.

## Why this patch

The maintainer's screenshot shows **Permissions: ordinary member** and
**This member changed since selection**. The loaded roster has no saved grant;
the selected draft version is stale. A save with that version would be rejected
with HTTP 412 and would create no successful history entry. The screenshot alone
does not show the actual save response, so this remains a concrete failure path
to verify, not a claim of a captured rejection.

## Changes

- Build 0.0.4.2 fixes a concrete ImGui ID collision: the potion-creation section
  header and its Create button previously used the same label/ID in one window.
  Distinct hidden IDs preserve their visible labels and allow the button to invoke
  the creation request. This addresses the report of a clickable button doing
  nothing; actual in-game production must still be rechecked.
- Member selection shows readable **Saved permissions** for character/home world.
  Checkboxes are explicitly labeled as draft changes, not proof of authorization.
- **Reload saved permissions from server** reads the active roster, including its
  pagination, and replaces the draft with current server values/version.
- **Save authorizations** is disabled when the loaded form version is stale. After
  reloading, edit the checkboxes and save. A race after reload can still be rejected
  by the server; its rejection is displayed beside the authorization controls.
- Save success displays **Saved by server** and the returned potion permission.
  **Authorization troubleshooting** shows the committed operation ID or safe error
  status/code/request ID/original action key. Read refresh does not overwrite this
  result. A timed-out/uncertain save retains the original action for reconciliation.
- **Create items → Create potion copies → Check my potion permission with server**
  reads the selected community's current permission directly with the member's session,
  then requests normal inventory refresh. This avoids requiring every ancillary
  community read to succeed before seeing the current permission.
- Owners see that access is automatic; missing recipes point to the owner editor.
  Insufficient points and invalid quantities explain why creation is disabled.

This uses existing server-authorized reads/writes; no client-side bypass or role
grant is introduced. Community/character/backend changes clear these diagnostic views.
`<3` support and recipe-message validation feedback remain M5, as requested.
Books/subtype classification, CRUD consolidation and structural interface work
remain in the [feedback plan](m3-feedback-review.md); this patch does not implement
new written subtypes or the full interface redesign.

## Install

Download the **AmorRP-plugin** artifact from the matching successful GitHub
**Plugin package** workflow run. Extract its outer ZIP, then the `latest.zip`
inside. Unload the old development plugin, extract the new plugin into a fresh
folder, and point Dalamud's development plugin location to the new `AmorRP.dll`.
Confirm **0.0.4.2** in the plugin installer and the **M3.1 potion creation fix**
window heading. Keep existing login/backend configuration and all adjacent DLLs.

## Focused live test

1. Owner: select the community, open **Members & currency**, select the affected
   active member, and click **Reload saved permissions from server**.
2. Check **Create potion copies**, then **Save authorizations**. Expect
   **Saved by server… Potion creation: Allowed** beside the controls. Expand
   **Authorization troubleshooting** to see its committed operation ID.
3. Click Reload again. The saved potion permission must still be **Allowed**.
   If it is not, report the inline save/check result and safe troubleshooting text.
4. Member: select the same community, open **Create items → Create potion copies**, and
   click **Check my potion permission with server**. Expect **allowed**. No logout
   or new XIVAuth login should be necessary for a capability change.
5. With an enabled recipe and enough current-week points, create one potion. If
   disabled, read the recipe/points/quantity explanation; permission and allowance
   are independent prerequisites.

If the owner reports Allowed and the member reports Not authorized, report both
character/home-world labels, community name, installed plugin version, timestamps and
safe results. Do not include tokens, invitation codes or private letter bodies.

## Verification

The new PostgreSQL integration scenario exercises stale-grant rejection with no
success history, reload/save, visibility through both member community-read routes
without relogin, actual potion creation, and same-key replay with one audit event.
The Windows plugin workflow builds and validates the actual API-15 ZIP. CI results
must be checked for the shipped commit; the live checks above remain required.

Maintainer follow-up on 0.0.4.1: permission works and letter creation works for
both members, but the enabled potion button produced no feedback. Source review
found the header/button ID collision above. Build 0.0.4.2 corrects it; live
production remains open until step 5 succeeds with the new version.

Final maintainer result: both participating characters successfully create/use
potions and messages post in chat on 0.0.4.2. M3/M3.1 is accepted for progression
to M4; the existing interface remains subject to structural redesign.
