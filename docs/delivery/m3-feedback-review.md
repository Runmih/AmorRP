# M3 live feedback and next-build scope

Recorded 2026-10-14 from the maintainer's testing and product review. This records
requirements, reported results and proposed work; it does not claim that a new
plugin/server build implements the changes below.

## Reported test results

**Final result:** after plugin 0.0.4.2, both participating characters successfully
create and use potions and messages post to game chat. The maintainer authorizes
progression to M4. The investigation below is historical; production and permission
are no longer open blockers. The interface is not accepted as the final layout.

- Item removal succeeds. The user could not select an item because the current
  interface uses plain text rows and a separate removal button on each row.
  Record the function as passed, with a remaining interaction defect.
- Member removal, unblocking and rejoining now work according to the maintainer.
  The current removal still includes blocking; successful execution does not
  constitute acceptance of that combined behavior.
- The maintainer reports that saving potion creation authorization does not give
  the target access. The initial owner report was subsequently traced to a recipe
  rejected because its literal use message contained `<3`; the plugin displayed
  no enabled recipe, not an owner-permission denial. Member production remains an
  open M3 acceptance blocker. Other flows were reported to
  work generally; do not infer individual quota/chat/persistence checks from that.
- After installing M3.1, the maintainer confirms **permission now works** and
  **letter creation works for both members**. Potion creation still fails and
  remains the open functional check. This supersedes the earlier access-failure
  report; do not declare production passed merely because the grant is effective.

## Potion-production investigation

Owner authority is implicit in both `GroupAccess.Action` and the plugin's owner
check. Owners do not grant themselves capabilities. They still spend their own
weekly allowance. An authorized member may create copies, not edit recipe
definitions. A usable enabled recipe and enough current-period points are also
required for production.

The source and earlier PostgreSQL tests exercise both owner production and
member production after authorization. That does not establish the live failure's
cause. Capture what the owner and member actually see: permission warning, missing
recipes, disabled button, or request rejection; remaining/current versus scheduled
points; recipe cost; and safe error code/request ID if a request is sent. Refresh
the affected member's view after a grant and inspect the authoritative capability
response. Do not call a quota or missing-recipe failure a permission failure, or
bypass authorization/weekly accounting to make the button work.

Latest reported messages: the owner saw "The owner has not enabled a potion
recipe yet"; the member saw "The owner must authorize potion creation for this
character." After discovering the rejected recipe, the maintainer still reports
no member access. The member's message is a local gate on the cached group's
`MyCapabilities`, not a server response to a production request. Check the owner's
authoritative **Permissions:** text after saving, Refresh data and reselecting the
member; do not use the checkbox as evidence of a persisted grant because its draft
is intentionally preserved across refresh. Record any save rejection separately.
If the persisted grant is present, compare the member's fresh same-group data and
refresh outcome; if absent, investigate the actual authorization save. No root
cause or corrective build is claimed from source inspection alone.

The supplied screenshot shows ordinary saved membership permissions and a
stale selected-member version. A grant using that version would be rejected;
the actual request response is not yet captured. The M3.1 plugin patch makes
saved values/results explicit, requires reload for a known-stale draft and offers
a direct member permission check. See the [patch guide](m31-install-test.md).
It is a diagnostic/corrective interaction patch, not proof of live resolution.

Subsequent live testing confirms the permission interaction works. Diagnose the
remaining production failure separately: whether the button is disabled, a
request is rejected, or success is reported without an item; current points,
recipe cost/revision and quantity; and safe error/request/operation IDs if sent.
Zero current potion points and next-week-only policy changes are existing rules,
not permission failures. Do not change quota accounting without evidence or an
explicit product decision.

The maintainer subsequently reports the potion button is enabled/clickable but
does nothing and produces no feedback. Source review identifies identical ImGui
IDs for `CollapsingHeader("Create potion copies")` and its nested Create button.
This is a concrete interaction defect. Build 0.0.4.2 assigns distinct hidden IDs
without changing labels, production permissions, quotas, recipes or server API.
It must be checked in game before declaring production passed.

## Literal use messages (M5)

The maintainer requires ordinary literal `<3` to be allowed and understandable
validation feedback when a potion definition cannot be saved. Both changes are
explicitly postponed to **M5**. Current server `ItemText.Chat` and plugin
`ChatCommand.TryCreate` reject angle brackets, explaining the recipe rejection.
Revise both consistently and verify creation, preview and actual game posting of
`<3`; preserve length/control-character limits and deliberate posting. Do not
label this rejection an owner-permission bug or silently schedule it for M3.1.

The UI currently disables creation when points are insufficient without a
specific explanation beside the button. The next corrective build must make
permission, recipe, quantity and allowance prerequisites understandable, including
explaining implicit owner access. Fix any confirmed refresh/grant/production defect
and repeat a live owner-plus-authorized-member creation check before accepting M3.

## Agreed resource behavior

Use CRUD for ordinary resources, with explicit domain actions where justified.
Voluntary leave and owner removal end the same membership; authorization and the
recorded actor differ. Both preserve holdings, balances, immutable identity and
weekly usage, revoke grants and permit invited rejoining. Blocking separately
ends membership and prevents rejoining. Unblocking permits invited rejoining but
does not restore membership or permissions automatically. Ownership protection
continues to apply.

The current runtime still has separate leave and combined remove/block commands.
The reviewed endpoint list describes that implementation and the proposed CRUD
replacement; no routes are changed by this planning update. Consolidation of
inventory removal, inventory lists and balance reads is proposed for M4. Personal
versus administrative history and redundant letter reads need further contract
review. Preserve authorization, private letter access and retry/version behavior
when consolidating routes.

## Books and written subtypes

The maintainer chose **Books** as the category, with **book**, **letter** and
**note** as subtypes. Replace the proposed Correspondence/Written Items naming in
the intended interface. This naming decision does not authorize arbitrary category
creation or define new book-specific features such as pagination. The current
runtime still has the `letter` item type and seeded Correspondence category;
document subtype contracts and compatibility before claiming all three exist.
Existing item/category IDs and letter content must survive any rename or migration.

## Interface structure

The current display is rejected as a final interface, not merely awaiting cosmetic
styling. Agree on navigation and user workflows before rebuilding it or adding
trading. The proposed first stage of M4 separates everyday inventory/currency/use
from owner/delegated administration, makes member actions understandable, and moves
login/device settings out of group history. History must identify actors and
affected characters by readable name/home world, alongside stable IDs and useful
action/quantity details; it must not expose private letter bodies or secrets.
Item removal needs selectable rows, a selected-item summary and a single clear
confirmation/action. Final styling/consistency may remain M5; functional clarity
and navigation cannot be postponed to that milestone.

## Next work

M3/M3.1 is accepted for progression. Follow the [M4 plan](m4-interface-plan.md):
vertical main navigation and horizontal subtabs, Inventory default, Home profile,
integrated authorized controls, History in Settings and stable background updates.
Then consolidate CRUD and implement trading in reviewable steps. Books naming
and subtypes belong to the new interface work; `<3` support remains M5.
