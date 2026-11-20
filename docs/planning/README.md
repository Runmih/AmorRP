# Current milestone plan

Updated 2026-11-20. This is the working plan; it replaces the old M0–M5 roadmaps.
Use main for source and distribution. Release numbers identify shipped packages,
not permission to mark a milestone accepted.

## Implemented baseline

Home, Inventory, Crafting and Writing Log share a tabbed window. Group management,
Season management, Trade and Settings have their own windows. The launcher opens
the matching tab or window, and Power controls automatic HUDs.

All verified players automatically belong to all non-deleted seasons, including
drafts and archives. One season is active; drafts and archives are read-only.
Economies and scoped grants remain separate. `seasons.create` is global and defaults
to the configured Lodestone administrator. Other players never see creation controls.
Season membership has no join, leave, removal, block or restore operation.

Identity/pairing, inventories, recipe revisions, quotas, two-stage trading, group
permissions, treasuries, trusted billing, debt collection, announcements and group
events are implemented. Backend rules remain authoritative. Export/deletion,
WebSocket delivery and later expansion features are not implemented merely because
OpenAPI contains a planned route.

## Milestones and evidence

| Item | Status | Acceptance or next evidence |
| --- | --- | --- |
| S01 group features and staff permissions | Accepted | Maintainer major game checks, 2026-10-29; automated authorization regressions |
| S02 accounts, treasury and disband settlement | Accepted | Maintainer four game checks, 2026-11-05; currency conservation/replay tests |
| S03 trusted billing and debt collection | Implemented; game acceptance pending | Check offline billing, trust revocation, debt policy, collection and disbanded-group returns |
| Seasonal direction and shared membership | Implemented; game acceptance pending | Administrator creation/activation, ordinary-user visibility, every season available, archive privacy and repeated refresh |
| Home tabs, launcher and Favorite Events | Implemented; game acceptance pending | Four navigation edges, launcher toggles, scaling, writing readability and empty HUD behavior |
| Trade grids | Implemented; game acceptance pending | Dragging and right-clicking stacks in both directions, search, offer limits and both confirmations |
| Maintenance consolidation | Automated verification complete | [0.4.0.4 evidence](../delivery/release-0.4.0.4.md): 381 tests, both exact-source CI workflows, shared foundations and current docs; no new game acceptance claimed |
| Operational readiness | Partial | Home host works per maintainer report; backup restore, recovery and capacity measurements still require operating evidence |
| Export/deletion and 1.0 readiness | Planned | Policy/retention decisions and tested private-data lifecycle; no destructive live processing implied |

Published package provenance is in packages/*.json. [History](../delivery/history.md)
records accepted checkpoints; [acceptance](../delivery/acceptance.md) is the current
manual checklist. Do not infer game or production acceptance from a successful build.

## Next work

1. Finish the focused S03/season/window game checks when the maintainer returns.
   Fix observed defects before adding new financial behavior. No new configuration
   flag or alternative community flow is needed.
2. Before S04, settle the tax destination, rounding and commission basis. Prepare
   an implementation checklist around existing settlement/receipt boundaries;
   snapshot allocations so later debt repayment cannot use changed rates.
3. Complete S04 as a bounded server/contracts/plugin slice, then verify it before
   enabling shop or permit spending. S07 gathering can proceed independently once
   its bounded configuration and budget rules are ready.
4. Keep integration adapters isolated. Discord linking is opt-in; Moodles failures
   are silent and never affect successful gameplay/accounting.

## Expansion dependencies

| ID | Planned work | Requires | Completion evidence |
| --- | --- | --- | --- |
| S04 | Taxes, cashier commission, basic clock-in | S03; tax/rounding decisions | Exact splits, saved allocation on partial repayment, cashier debt and permission changes |
| S05 | Season shop | S02/S03; revenue destination | Atomic affordable purchases, replay-safe outcomes and buyer authorization |
| S06 | RP ownership permits and release | S03/S05 | Initial OOC acceptance, full quote, atomic settlement, owner-only release and disband release |
| S07 | Gathering packages and budget bonuses | S01/inventory | Shared weekly production budget, costs, receiving limits, consistent reset and replay-safe use |
| S08 | Classes and reset potion | S05/S07 | Slot limits, permanent choices until reset, no weekly-budget refill exploit |
| S09 | Group-restricted recipes | S01/S07 budget rules | Membership and recipe/crafting authority remain separate; bounded durable limits |
| S10 | Silent optional Moodles | Consumable use; S06 for permit effect | Best-effort IPC; absence/rejection never blocks a committed action or displays errors |
| S11 | Discord writing earnings | S01/S03; home host | Opt-in linking, weekly/balance limits, account selection, deduplication and automatic unlink |

Detailed requirements: [decisions](decisions.md), [economy](economy-and-groups.md),
[gameplay](gameplay.md), [integrations](integrations.md). Nice-to-have work must not
block independent required work. Later group item storage follows S02; permit
trading follows S06 and a defined registration/billing-lock transfer rule. Schedules
and per-staff rates are separate follow-ups.

User/account moderation remains separate planned work; do not implement bans by
changing season membership. Incremental future extraction of SeasonsPanel should
follow feature-owned state and regression coverage, not a broad controller rewrite.

## Working rules

Reuse feature services, transactions, receipts, settlement and query state. Extract
shared code after real duplication appears; keep security ordering visible. Do not
introduce a generic workflow engine, speculative layers or empty future folders.
Update rules/API/acceptance with behavior. Preserve applied migrations, stable IDs,
protected local settings and immutable published packages. User input is needed
only for material unresolved product decisions, live game checks and operating
steps unavailable from this workspace.
