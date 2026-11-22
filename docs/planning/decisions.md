# Expansion decisions

Accepted feature requirements from 2026-10-29, updated for Seasons on 2026-11-16.
S01–S07 are implemented. S08–S11 remain planned. Follow the [milestone plan](README.md).
Every verified player belongs to every season. No season join/leave/block controls
exist; user bans belong to separate account moderation. Creation requires seasons.create.

## Accepted requirements

| ID | Decision |
| --- | --- |
| D01 | A season is the currency/permission boundary. Groups are owned subgroups inside it. Keep the existing three-owned-group limit per season and no numeric limit on groups joined; existing open/closed/password joining rules still apply. Creation still requires the season's Create Groups permission. |
| D02 | Season owners choose which groups receive Treasury access and Discord Bot Permission. Creating a group does not enable either feature. |
| D03 | Group owners assign staff permissions within season-approved features. They cannot grant their group a feature withheld by the season. Existing Manage Groups remains deletion-only; private group content is not opened to season moderators. |
| D04 | Organize permissions and hide unauthorized tabs. Do not add permission-source explanations or locked-feature tabs. Classes grant gameplay permissions, not moderation powers. |
| D05 | A character's Trusted switch authorizes automatic billing by that group, including while the character is offline. There is no payer confirmation per bill and no player-facing amount/spending cap. Deliberate overcharging can be RP. |
| D06 | Protect against accidental/retried duplicate bills with durable bill identity. A proposed short cooldown is additional double-click protection, not the correctness mechanism. |
| D07 | Season owners decide whether character debt is enabled. An uncovered bill can create debt under that policy without a separate debtor approval. Record receipts. Debt is not newly created spendable currency. |
| D08 | Collect character debts automatically using full available funds/income, without a protected personal allowance. Preserve existing active-trade reservations. |
| D09 | Use cashier commission, not a shift pool. The group owner sets the percentage; the cashier receives their share and the treasury receives the remainder of collected proceeds. |
| D10 | Add group currency treasury, later group item storage, season taxes, group staff permissions and basic clock-in. Shift scheduling and more elaborate payroll can follow later. |
| D11 | On group disbanding, remaining treasury assets transfer to the group's owner. The deleting moderator does not become the beneficiary. Preserve receipts and assets. |
| D12 | A season shop supplies owner-controlled, priced products such as the planned reset potion. Its Slave tab lists consenting characters. Registration issues a permit in the character's name. |
| D13 | A dedicated season moderator permission authorizes submitting characters for the Slave list. The affected player must consent OOC before publication and accept the specific group before registration. Private offers target a specific buyer without a public listing. The owning group must afford the base price and payment of all existing character debts. |
| D14 | While enslaved, automatic billing authorization for the owning group remains enabled. The affected character cannot avoid it by toggling Trusted off. |
| D15 | The owning group owner releases the character by destroying/deleting the active permit. Ordinary treasury/item access does not grant release authority. Group disbanding releases all affected characters. Release preserves outstanding debts and previous payments. |
| D16 | Keep ownership RP player-managed: a registered document/tag, owning group and billing lock. No automation of character actions. Permit trading is a later expansion, not an ordinary item transfer in the first version. |
| D17 | A dedicated Gathering and Hunting gameplay permission grants a shared weekly budget that resets to the configured maximum with crafting. Show the bar for eligible players in Home → Your resources using the existing reset timer. Players spend points to make delivery packages they can use or trade. Season settings control package costs and point grants. |
| D18 | Start with Alchemy and Cooking supplies. Packs grant their full bonus even above the base weekly crafting maximum so players can afford advanced recipes. Unused bonus points expire at the shared weekly reset. Future crafting disciplines use the same package and budget rules. |
| D19 | Season owners set the number of class slots, initially one. Chosen classes are permanent until a default Alchemy reset potion is used. The potion is expensive; switching never refills used budgets. |
| D20 | Group-only recipes are wanted, with limits. Membership, definition permission and crafting permission remain distinct checks. |
| D21 | Moodles runs automatically when available: no Amor RP opt-in, installation requests, setup prompts, missing-plugin messages or failure notifications. Silently skip failure; bounded Dalamud debug logging is permitted. No Moodles failure blocks gameplay/accounting. |
| D22 | Consumed items can carry defined Moodle effects. Active slave status continually attempts its configured debuff. Losing the visual effect never releases server-side ownership or billing authorization. |
| D23 | Discord earnings are fully opt-in. One season can connect multiple Discord servers through groups with Discord Bot Permission. Only the permitted group owner can initiate Amor RP linking; Discord administrators still control installation/channel/role authority. |
| D24 | Use one selected earning character per Discord account in a season. Changing character or connected server must not reset earning usage. |
| D25 | Discord rewards have weekly limits and an owner-configurable personal balance ceiling. Stop rewards at the ceiling; spending can resume eligibility. Owners have enough controls to tolerate treasury deposits lowering personal balance. |
| D26 | Disbanding a linked group disables earnings and ejects the bot from that Discord server. The private Ubuntu host is already in use; Discord integration remains planned. |
| D27 | The future private server uses Ubuntu on the maintainer's GMKtec NucBox M8 (Ryzen 5 PRO 6650H, 16 GB RAM, 512 GB storage). Setup/cutover is future work; do not provision it now. |
| D29 | Confirmed 2026-11-05: unpaid bills survive group disbanding and future repayments belong to its former owner, delivered through the existing Treasury returns path. |
| D28 | Use main only and application versions 0.feature.0.patch: feature releases increment the second part, patches increment the fourth, first/third stay 0. No new feature/testing/milestone branches or testing feed; no application-version bump for documentation alone. |

## Proposed implementation defaults

These are engineering recommendations, not additional maintainer decisions. Revisit
only the relevant entries when their feature starts; none requires a question now.

| Topic | Proposed default | Resolve by |
| --- | --- | --- |
| Debt order | Implemented: oldest unpaid bill first, with stable tie-breaking and full collection of unreserved funds. | S03 complete; game acceptance pending |
| Tax and commission basis | Apply season tax to collected service proceeds, then calculate cashier commission on the remainder. Round both down against cumulative collections and save rates on the bill. | S04 |
| Tax revenue destination | Credit tax to the season treasury and show the split on each receipt. | S04 |
| Staff cut | One group-wide commission percentage, paid to the issuing cashier. Per-staff rates are later. | S04 |
| Clock-in | Simple on/off staff state and optional group requirement for billing. No scheduled shifts or paid time accrual initially. | S04 |
| Shop payment | Require actual available funds; ordinary shop purchases do not create debt. Shop revenue credits the season treasury. | S05 |
| Private ownership offer | Start from a trade action and show the named buyer, owning group, price, and exact transferable items and available currency. Both players confirm the same quote. The group receives the named permit and the buyer receives the agreed assets. | S06 |
| Reset potion price | Season shop price supplies the hefty cost. Consume/charge only when replacement classes are confirmed; preserve current classes on cancellation. | S08 |
| Changed class slots | Increasing slots permits additional selection; reducing slots offers affected players a correction without a paid reset. Exact transition is feature-time detail. | S08 |
| Group recipe behavior | Restrict crafting to members; finished items remain tradeable. Resolve recipe visibility and numeric limits when implementing. | S09 |
| Discord association | One Discord server per group and one group per Discord server. Several groups allow several servers per season. This makes automatic bot ejection unambiguous. | S11 |
| Hoarder calculation | Personal owned balance includes reserved currency, excludes treasury savings, and does not subtract debt. Clamp the last reward to remaining headroom. | S11 |
| Discord counters | Account/season earning usage spans linked servers and selected characters; season-wide payout budget also bounds minting. | S11 |

## Questions and changes

Financial claim entitlement was settled for S03 on 2026-11-05 (D29).
Permanent-host readiness remains a gate for S11. Those can wait until
that work starts. Do not turn every numeric default, label or control arrangement
into a new approval request.

Change accepted rows only when the maintainer changes the requirement; record the
reason/date. Keep proposed defaults visibly separate from accepted behavior.
Never silently reintroduce per-bill confirmations, voluntary debt approval,
Moodles setup/error messages, self-service permit deletion or a shift pool.
