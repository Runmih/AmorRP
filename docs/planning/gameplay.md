# Gameplay expansion

Planned S07–S09. Read the [decisions](decisions.md) and [work order](README.md).
Current consumable/writing inventory and community/group membership already exist;
new budgets/classes must preserve their authority and item provenance.

## S07 — Gathering and delivery packages

The maintainer chose a simple weekly-point button action. Players choose a delivery
package, spend gathering points, receive its item, then RP and trade the delivery.
No actual FFXIV gathering detection, timed runs, daily claims or minigame is required.
A combined Gathering permission covers hunting and gathering for this version.

Community settings define:

| Setting | Purpose |
| --- | --- |
| Weekly gathering points | One shared production budget across package choices |
| Cost per package | Gathering points spent for that discipline's delivery |
| Points granted per consumed package | Bonus crafting points in its discipline |
| Weekly bonus receiving limit | Prevent unlimited points being funneled to one crafter |
| Bonus expiry/reset policy | Clear remaining-point behavior at the weekly boundary |

Alchemy Supplies grant Alchemy points; Cooking Supplies grant Cooking points. Package
creation/use is atomic and replay-safe. Quantities, consumption and actual grant
are server-validated; a client cannot submit arbitrary bonus points. Decide whether
an over-limit use is rejected or grants a clamped amount before exposing the use UI.
The item must not disappear without the player receiving the defined outcome.

Use stable discipline identifiers for budgets, permission associations and package
choices. Investigate the current explicit potion/cooking/writing fields before choosing
a bounded extension; do not copy the entire system for each new discipline or rewrite
all crafting into a generic engine. Existing recipe/holding snapshots stay valid.

Track weekly spending and received bonuses independently of current class selections.
Disbanding, leaving/rejoining, new package IDs and class changes cannot reset usage.
Numeric defaults and bonus expiry are feature-time settings; ask only if a proposed
rule materially changes the agreed economy.

Verification: shared production limit, correct discipline, configurable costs,
concurrent create/use, duplicate consumption, trading reservations, reset boundary,
receiver limit, old snapshots and no budget refill through identity/group changes.

## S08 — Classes and reset potion

Community owners configure available classes, their gameplay grants and class-slot
count. Initial default is one slot; the model must support more later. Class selection
is performed by the player. A selected class remains fixed until a reset potion is
used. Classes do not grant moderation, group feature approval or Discord administration.

Combine class-provided gameplay access with existing explicit staff-managed gameplay
grants without silently deleting those grants. Exact precedence/owner restrictions
belong in the S08 checklist. The authoritative result is checked on the server and
unavailable tabs stay hidden. No new permission-source explanation interface.

Supply a default Alchemy reset potion with an expensive community-controlled price;
the community shop is the proposed source. A sale by another player must not
accidentally become a free reset endpoint. Recipe availability/other suppliers can
be decided when S08 starts, keeping the agreed hefty cost meaningful.

Proposed reset flow: show the replacement selection and actual cost/outcome, then
commit selection, payment/item consumption and receipt together. Cancellation keeps
the old classes/item. Repeat requests cannot charge/consume again. Preserve used
weekly budgets and bonus receiving counters across every class change.

Increasing slot count allows more selections. Decreasing it needs an explicit
transition for existing characters; proposed default is a free corrective selection,
not randomly deleting a class or charging for an owner policy change. This is a
feature-time decision rather than a reason to block earlier economy work.

Verification: unauthorized class grants, slot limits, concurrent selection/reset,
current grants versus class grants, cancellation, affordability, replay, owner policy
changes and preservation of weekly usage.

## S09 — Group-only recipes (nice to have)

Crafting requires active community/group membership and the normal discipline crafting
permission. Defining a recipe requires the relevant recipe-definition authority;
group ownership alone does not grant withheld community authority. Community owners
or Manage Groups moderators receive no automatic access to private group content.

Add configurable limits for active group recipes. Define whether retiring/deleting
recipes frees capacity and prevent recreate cycles from evading production limits.
Keep authoring limits distinct from the character's weekly crafting points.

Proposed first version: finished goods remain tradeable normally and use existing
character point budgets. Recipe visibility, numeric caps, ownership after author
removal and enabled disciplines are details to settle for S09. No new group-wide
crafting budget, research tree or recipe marketplace is implied.

Verification: outsiders/moderators denied where appropriate; membership removal;
permission changes; concurrent creation/retirement; cap enforcement; existing goods
and immutable revisions surviving recipe retirement/group deletion.
