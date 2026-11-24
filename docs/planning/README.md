# Current milestone plan

Updated 2026-11-22. This is the working plan; it replaces the old M0–M5 roadmaps.
Use main for source and distribution. Release numbers identify shipped packages,
not permission to mark a milestone accepted.

## Implemented baseline

Home, Inventory, Crafting and Writing Log share a tabbed window. Group browsing
and management both live under Home → Groups. Season management, Trade and
Settings have their own windows. The launcher opens
the matching tab or window, and Power controls automatic HUDs.

All verified players automatically belong to all non-deleted seasons, including
drafts and archives. One season is active; drafts and archives are read-only.
Economies and scoped grants remain separate. `seasons.create` is global and defaults
to the configured Lodestone administrator. Other players never see creation controls. Server administrators grant this under Season Management → Members.
Season membership has no join, leave, removal, block or restore operation.

Identity/pairing, inventories, recipe revisions, quotas, two-stage trading, group
permissions, treasuries, trusted billing, debt collection, announcements and group
events are implemented. Billing includes season tax, cashier commission and clock-in. Backend rules remain authoritative. Export/deletion,
WebSocket delivery and later expansion features are not implemented merely because
OpenAPI contains a planned route.

## Milestones and evidence

| Item | Status | Acceptance or next evidence |
| --- | --- | --- |
| S01 group features and staff permissions | Accepted | Maintainer major game checks, 2026-10-29; automated authorization regressions |
| S02 accounts, treasury and disband settlement | Accepted | Maintainer four game checks, 2026-11-05; currency conservation/replay tests |
| S03 trusted billing and debt collection | Implemented; game acceptance pending | Check offline billing, trust revocation, debt policy, collection and disbanded-group returns |
| S04 tax, commission and clock-in | Implemented; game acceptance pending | Verify clock-in, owner clock-out, payment receipts and all three destinations across a rate change |
| Seasonal direction and shared membership | Implemented; game acceptance pending | Administrator creation and activation, active season rollover for players, archive privacy and repeated refresh |
| Home tabs, launcher and Favorite Events | Implemented; game acceptance pending | Four navigation edges, launcher toggles, scaling, writing readability and empty HUD behavior |
| Trade grids | Implemented; game acceptance pending | Dragging and right-clicking stacks in both directions, search, offer limits and both confirmations |
| Maintenance consolidation | Automated verification complete | [0.4.0.4 evidence](../delivery/release-0.4.0.4.md): 381 tests, both exact-source CI workflows, shared foundations and current docs; no new game acceptance claimed |
| Operational readiness | Partial | Home host works per maintainer report; backup restore, recovery and capacity measurements still require operating evidence |
| Export/deletion and 1.0 readiness | Planned | Policy/retention decisions and tested private-data lifecycle; no destructive live processing implied |

Published package provenance is in packages/*.json. [History](../delivery/history.md)
records accepted checkpoints; [acceptance](../delivery/acceptance.md) is the current
manual checklist. Do not infer game or production acceptance from a successful build.

## Next work

1. Finish the focused S03/S04, season, and window checks in game.
2. Verify the Shop, permits, Gathering, class reset, group recipes and optional Moodles in game. Classes, group recipes and Moodles are implemented in the local 0.11 build.
3. Verify the local 0.16 build in game, including the staff tools and permit transfer
   screens. Run Discord linking, channel setup, payout and removal with a test
   Discord installation before enabling it for players.
4. Continue 1.0 readiness work on account moderation, data export and deletion,
   and backup restore testing.

## Expansion dependencies

| ID | Work | Requires | Completion evidence |
| --- | --- | --- | --- |
| S04 | Taxes, cashier commission, basic clock-in | S03; tax/rounding decisions | Exact splits, saved allocation on partial repayment, cashier debt and permission changes |
| S05 | Season shop (0.6), with a Shop tab between Home and Inventory | S02/S03; revenue destination | Shared-window navigation, atomic affordable purchases, replay-safe outcomes and buyer authorization |
| S06 | RP ownership permits and release (0.7) | S03/S05 | Moderator listings, private offers, explicit OOC consent, atomic registration, owner-only release and disband release |
| S07 | Gathering packages and budget bonuses (0.8) | S01/inventory | Dedicated gameplay permission, shared weekly reset, advanced recipes above the base allowance and replay-safe use |
| S08 | Classes and reset potion (0.9) | S05/S07 | Slot limits, permanent choices until reset, no weekly-budget refill exploit |
| S09 | Group-restricted recipes (0.10) | S01/S07 budget rules | Membership and recipe/crafting authority remain separate; bounded durable limits |
| S10 | Optional Moodles (0.11) | Consumable use; S06 for permit effect | Best-effort IPC; absence/rejection never blocks a committed action or displays errors |
| S11 | Discord writing earnings (0.12) | S01/S03; home host | Implemented locally; live Discord acceptance pending |
| S12 | Group item storage (0.13) | S02 and inventory revisions | Implemented locally; storage grants, atomic moves and disband returns |
| S13 | Permit transfers (0.14) | S06 and group treasury | Implemented locally; directed consent, treasury payment and billing-lock transfer |
| S14 | Staff shifts (0.15) | S04 clock-in and staff | Implemented locally; assigned, non-overlapping shifts and owner cancellation |
| S15 | Individual cashier commission (0.16) | S04 bill allocation | Implemented locally; optional staff rates are pinned on new bills |

Detailed requirements: [decisions](decisions.md), [economy](economy-and-groups.md),
[gameplay](gameplay.md), [integrations](integrations.md). Nice-to-have work must not
block independent required work. [The 0.16 local build](../delivery/release-0.16.0.0.md)
includes the five feature milestones. The running server and published installer
feed use the latest separately released version.

User/account moderation remains separate planned work; do not implement bans by
changing season membership. Incremental future extraction of SeasonsPanel should
follow feature-owned state and regression coverage, not a broad controller rewrite.

## Nice to have

- **Tomes & Scrolls** comes after the other planned work. Add usable skill books,
  spell tomes and scrolls, then show their inventory tab. Define item effects and
  how permanent learning is recorded before adding those uses.

## Working rules

Reuse feature services, transactions, receipts, settlement and query state. Extract
shared code after real duplication appears; keep security ordering visible. Do not
introduce a generic workflow engine, speculative layers or empty future folders.
Update rules/API/acceptance with behavior. Preserve applied migrations, stable IDs,
protected local settings and immutable published packages. User input is needed
only for material unresolved product decisions, live game checks and operating
steps unavailable from this workspace.
