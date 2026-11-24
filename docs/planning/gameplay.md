# Gameplay expansion

S07–S09 are implemented with game acceptance pending. Read the [decisions](decisions.md) and [work order](README.md).
Current consumable/writing inventory and season/group membership already exist;
new budgets/classes must preserve their authority and item provenance.

## S07 — Gathering and delivery packages (0.8)

The maintainer chose a simple weekly-point button action. Players choose a delivery
package, spend gathering points, receive its item, then RP and trade the delivery.
No actual FFXIV gathering detection, timed runs, daily claims or minigame is required.
A dedicated **Gathering and Hunting** gameplay permission grants access to a shared
weekly budget for both activities. The budget resets to the season's configured
maximum at the same Monday 00:00 UTC boundary as crafting. Unused Gathering and
Hunting points expire at that reset.

In **Home → Your resources**, show the remaining points and weekly maximum as a
resource bar alongside the crafting bars when the player has the Gathering and
Hunting permission. Use the existing shared reset timer.

Season settings define:

| Setting | Purpose |
| --- | --- |
| Weekly gathering points | One shared production budget across package choices |
| Cost per package | Gathering points spent for that discipline's delivery |
| Points granted per consumed package | Bonus crafting points in its discipline |

Alchemy Supplies grant Alchemy points; Cooking Supplies grant Cooking points. Package
creation/use is atomic and replay-safe. Quantities, consumption and actual grant
are server-validated. Consuming a package grants its full point value, even when
the resulting balance exceeds the configured base weekly crafting maximum. There
is no separate weekly receiving cap. Advanced recipes can cost more than the base
weekly allowance. Show the actual available points in crafting and allow the
player to spend the combined balance when they can cover the cost.

For example, a character whose base Alchemy allowance is 100 can consume a
30-point pack and spend 120 points on an advanced recipe. At the next shared
weekly reset, unused bonus points expire and the crafting balance resets to the
configured base maximum.

Use stable discipline identifiers for budgets, permission associations and package
choices. Investigate the current explicit potion/cooking/writing fields before choosing
a bounded extension; do not copy the entire system for each new discipline or rewrite
all crafting into a generic engine. Existing recipe/holding snapshots stay valid.

Track weekly spending and received bonuses independently of current class selections.
Disbanding, leaving/rejoining, new package IDs and class changes cannot reset usage.
Numeric defaults for budgets, package costs and grants remain feature-time settings.

Verification: shared production limit, correct discipline, configurable costs,
concurrent create/use, duplicate consumption, trading reservations, reset boundary,
full bonuses above the base weekly maximum, crafting a recipe above that maximum,
bonus expiry, old snapshots and no budget refill through identity/group changes.

## S08 — Classes and reset potion

Season owners configure available classes, their gameplay grants and class-slot
count. Initial default is one slot; the model must support more later. Class selection
is performed by the player. A selected class remains fixed until a reset potion is
used. Classes do not grant moderation, group feature approval or Discord administration.

Combine class-provided gameplay access with existing explicit staff-managed gameplay
grants without silently deleting those grants. Exact precedence/owner restrictions
belong in the S08 checklist. The authoritative result is checked on the server and
unavailable tabs stay hidden. No new permission-source explanation interface.

The 0.17 Character Reset Potion replaces the earlier Class reset tab. The
fixed product is sold under Shop → Products. The owner controls its price and
per-character season limit under Manage products. Using it refunds allocated
ability points and clears the selected classes, while preserving earned bonus
points and weekly usage.

Players can fill new or empty slots without payment. If the owner removes a class
or lowers the slot count, the player can make the necessary correction for free.
Eligible existing choices stay in place until the player confirms a change.

Verification: unauthorized class grants, slot limits, concurrent selection/reset,
current grants versus class grants, cancellation, affordability, replay, owner policy
changes and preservation of weekly usage.

## S09 — Group-only recipes (nice to have)

Crafting requires active season/group membership and the normal discipline crafting
permission. Defining a recipe requires the relevant recipe-definition authority;
group ownership alone does not grant withheld season authority. Season owners
or Manage Groups moderators receive no automatic access to private group content.

The limit defaults to 10 non-retired recipes per group and is configurable in
Season Management → Classes. Recipes keep their original group scope. Retirement
frees a slot. Existing finished goods remain usable and tradeable, and production
spends the character's normal weekly points. Eligible authors can continue
maintaining a recipe after its original author leaves. Only active group members
with the relevant crafting or authoring grant can browse restricted recipes.

Verification: outsiders/moderators denied where appropriate; membership removal;
permission changes; concurrent creation/retirement; cap enforcement; existing goods
and immutable revisions surviving recipe retirement/group deletion.
