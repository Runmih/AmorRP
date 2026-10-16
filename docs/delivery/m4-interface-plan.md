# M4 interface and implementation plan

Updated 2026-10-15 following M3 acceptance and the maintainer's interface direction.
This is the agreed M4 direction. Implementation is now on `m4-trading`; see the
[M4 installation and acceptance guide](m4-install-test.md). Game acceptance remains open.

## Accepted baseline

After plugin 0.0.4.2 the maintainer confirms both participating characters create
and use potions, posting the potion message to game chat. Earlier checks confirmed
permissions, letters, item/member removal, unblocking and rejoining. M3 is accepted
for progression to M4. The current interface is not accepted as the final product;
full 1.0 operational/release checks remain M5. Do not add paid rename tests or
invent additional milestone gates.

## Navigation

The correction is authoritative: **vertical main navigation, horizontal subtabs**.
Inventory is selected by default. The group selector stays outside section content,
with verified character and currency balance always visible. Keep the compact
consumables window as a separate RP-friendly view.

| Main section (vertical) | Content and horizontal subtabs |
| --- | --- |
| Home | Group profile: name, description, currency identity and group information. Owner edit/invitation actions sit beside the relevant content. |
| Inventory (default) | All, Consumables, Books. Stable list and selected-item details/actions. Books distinguishes book, letter and note. |
| Crafting | Potions and Books. Copy creation and written-item creation are separate from browsing inventory. Owner recipe create/edit buttons live in the potion recipe area. |
| Members (proposed supporting section) | Roster with authorized actions on the selected member: adjust currency, manage inventory, permissions, Remove, Block and Unblock. |
| Trades | Pending invitations, active trade and recent outcomes. Context-menu initiation opens the relevant workflow. |
| Settings | Plugin settings, login/device management and a tucked-away History subtab. Owners get administrative history; ordinary users get personal history. |

Members is a proposed location for roster workflows, not an Owner section. Hide
unauthorized controls in every view; keep prerequisite explanations such as points
and reservations beside available actions. The server rechecks authority even when
the interface previously showed a button. No Owner-only main tab.

```text
Amor RP       [Group v]    Character @ Home World       [coin] Balance    [Sync]
┌─────────────┬──────────────────────────────────────────────────────────────┐
│ Home        │ Inventory                                                    │
│ Inventory ● │ [All] [Consumables] [Books]                                  │
│ Crafting    │ [Search...]                         [Subtype / Sort]         │
│ Members     │ ┌───────────────────────────┬──────────────────────────────┐ │
│ Trades      │ │ Name       Available      │ Selected item                │ │
│ Settings    │ │ Item rows                 │ Description / author         │ │
│             │ │                           │ Quantity / exact use message │ │
│ [Compact]   │ │                           │ Relevant actions             │ │
│             │ └───────────────────────────┴──────────────────────────────┘ │
└─────────────┴──────────────────────────────────────────────────────────────┘
```

Use the supplied Lightless screenshot as inspiration for tidy rows, spacing,
separation and contextual controls. A public client source reference is
[LightlessClient](https://github.com/defnotken/LightlessClient). Do not introduce
its mod-sync/presence features or copy branding/assets. Pair icons with clear text.

## Stable updates

M3's shared work flag disables the whole product during requests, every mutation
asks for a full group refresh, and accepting inventory data clears the member's
inventory inspector. These contribute to the reported jarring behavior; the report
does not establish that the plugin actually unloads/reloads itself.

- Separate background reads, mutations and local display state. Keep the current
  screen visible; reads do not freeze navigation or editing.
- Preserve selected item/member/recipe, open panels, scroll, filters and drafts.
  Never silently adopt a new edit version for an old draft.
- Apply authoritative mutation responses to affected data, then refresh that data
  quietly. Clicking an action must not restart all group/device/catalog requests.
- Disable the affected write action while pending. Unresolved mutations retain
  their original recovery key; don't optimistically assume success.
- Keep quiet 60-second inventory/group refresh while relevant views are visible.
  Trade updates may be more frequent but update only trade data, not the whole view.
  Bound requests, avoid overlap and back off on failure.
- Preserve action feedback beside its action; background sync never overwrites it.
  A small status indicator communicates freshness/offline state.
- Clear private data immediately on character/backend/group/access changes; reject
  late responses from an old scope and reconcile versions for overlapping reads.
- Give simultaneously drawn widgets distinct stable IDs, including nested forms.

## Member and item interactions

Select member → choose permitted action → review target/quantity/reason → confirm
→ preserve selection and show outcome. Leave and owner removal end the same
membership. Blocking separately prevents return; unblocking only restores join
eligibility. Preserve assets/usage and require new grants after rejoining.

Select item → inspect → Use, Read/Edit, Discard or Trade as applicable. Authorized
member-inventory management uses summaries, selectable rows, selected-item details
and one clear removal action. It never grants access to private written bodies.

History belongs in Settings and identifies actor/affected character by readable
name/home world, action, quantity/currency change, time and reason. Retain permanent
IDs underneath; no names as ownership keys, private bodies or authentication secrets.

## Implementation sequence

1. Establish navigation and stable update infrastructure. Retain existing data,
   credentials and once-per-startup chat consent. Implement Books classification
   with book/letter/note without inventing pagination or new RP mechanics.
2. Consolidate ordinary membership and inventory/balance operations, separate
   blocking, and preserve authorization/retry/version behavior. Document compatibility
   during transition; update OpenAPI and endpoint catalog with the implementation.
3. Implement persistent same-group trading: invitation, offers, reservations,
   revision-specific confirmations, atomic completion, cancellation/expiry and
   restart/unknown-outcome recovery. Removal/restriction cancel affected trades.
4. Integrate character-context initiation and two-column trade review. No public
   player lookup or plugin-user presence discovery. Show sealed written summaries
   and exact potion previews from server-authorized trade responses.
5. Verify meaningful accounting/authorization/recovery scenarios with PostgreSQL,
   build plugin/server, and provide a focused two-character live test.

## Exit checks

- Correct vertical navigation/horizontal subtabs, default Inventory, profile Home,
  dedicated creation, hidden History and integrated authorized controls.
- Reads/actions preserve selection, drafts, scroll and view. Offline/late responses
  do not expose another context's private data.
- Leave/removal permits invited rejoining; block prevents it; unblock doesn't
  restore membership. Item removal does not refund creation allowance.
- Context-menu trading is same-group only. Both players confirm the same offer
  revision at both stages; editing after either acceptance cancels the deal.
- Reservations prevent double spending/use/discard/edit. Currency supply and item
  ownership remain consistent after exchange/cancellation.
- Disconnect/restart/retry cannot duplicate transfer or lose reservations.
  Completion and member removal/restriction serialize safely; outcomes are recoverable.

Literal `<3` support and recipe-message validation feedback remain M5 by request.
Final styling/refinement and 1.0 operations remain M5; interaction correctness is M4.

The 0.0.5.2 live-feedback revision moves trading into a separate window: inventory
on the left, own offer above peer offer on the right, arrows for item quantities,
and currency saving on focus loss. No Publish/Reload workflow remains. Both accept
then both approve the final popup. Editing after either acceptance cancels the
entire deal. See the updated [M4 test guide](m4-install-test.md).
