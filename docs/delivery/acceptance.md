# Acceptance and verification

Automated checks, game checks and operating evidence are separate. Only record a
manual pass after an actual maintainer/tester result. The [milestone plan](../planning/README.md)
records current acceptance status; this checklist describes what to verify.

## Player feature checks

| Feature | Acceptance |
| --- | --- |
| F01 identity | Fresh browser pairing, wrong/shared code rejected, saved login renewal, mismatch preserves stable assets |
| F02 seasons | New and returning characters follow the active season. Server administrators can browse drafts and archives and activate drafts from Season Management. |
| F03 permissions | Ordinary user sees no season creation; administrator default works; explicit grant/revoke; no group/season escalation |
| F04 currency | Accurate owned/reserved/available balances; authorized adjustments; treasury conservation and actual-owner disband returns |
| F05 types | Alchemy/Cooking and book/letter/note remain distinct; unknown type fails safely |
| F06 categories | Allowed types and organization persist; category changes never grant crafting privileges |
| F07 definitions | Author permissions/revisions/retirement; cost changes invalidate previews; old copies remain readable |
| F08 production | Atomic quantity/cost, separate character/season budgets, Monday reset, no refresh/grant-change refill |
| F09 writing | Full dedicated reader; holder-only body; bounded input; eligible edits and traded-text lock |
| F10 inventory | Launcher opens the Inventory tab in Home; tabs/filter/sort/pins/details and finances stay readable; vanilla inventory has no effect |
| F11 use/chat | Literal `<3`, correct destination/preview, exactly one item mutation, no automatic chat retry or startup permission window |
| F12 trade | Requests, drag and right-click in both directions, partial stacks, line limit, read-only partner grid, both approval stages, stale revisions, cancellation/expiry, close/reopen and reservations |
| F13 history | Personal/participant records only; owner audit; no other player's private body or receipt |
| F14 recovery | Restart and lost-response replay preserve operations, balances, reservations and unfinished drafts |
| F15 settings | Interface/Account/About; launcher open/close toggles, equal-sized power icon and shared window style; individual panel preferences; origin-bound saved state |
| F16 lifecycle | Install/update/version errors, readiness, migration/restore evidence; export/deletion still planned |
| F17 profiles/notices | Profile/logo edits, five retained announcements/read markers, group privacy, notices/events/favorite HUD |

## Focused outstanding game checks

- S03: trust a group, bill offline, revoke trust, test debt disabled/enabled,
  collection on income/reservation release, and disbanded-group repayment returns.
- S04: set tax and commission, require clock-in, then verify the composer unlocks
  after clock-in and locks after owner clock-out. Pay a bill in installments across
  a rate change. Check each receipt, both treasuries, and the cashier's balance.
- Character context: right-click a player, open ♥ Amor RP, and verify Trade
  immediately sends a request to that player. Open Billing, charge a trusted customer, and check amount,
  service or item, issuing group and payment receipt. Verify the bottom Return row
  reopens the game menu for that player. Check that billing requires a group grant.
- Home → Groups: open the directory from Home, the flag and `/amorrp groups`.
  As group owner, select a member to change permissions or clock them out.
  Check that members can view the roster and nonmembers can join from Members.
- 0.6 Shop: create and hide a listing, test personal and group purchases and
  verify inventory delivery, stock, buyer limits and season treasury receipts.
- Permits: request a public listing and approve it from the listed character.
  Buy from the Slave name and Price table. Check automatic group selection for a
  single eligible group, price changes, concurrent buyers, debt settlement,
  release and disband. Verify that one Accept settles the private offer, stale
  terms leave funds and assets intact, and a replay charges once.
- 0.8 Gathering: grant the permission, configure supplies, gather and trade
  both package types, use one to craft an item above the base allowance, and
  verify the shared reset clears unused bonus points.
- 0.9 Classes: choose a class as a normal player, check that unavailable disciplines
  disappear, and grant a second discipline manually. Cancel a class reset, then
  buy it from Shop → Class reset. Verify the price, both grants, and the weekly
  points. Lower the slot count as owner and verify the free correction.
- 0.10 Group recipes: create a scoped recipe, craft it as a permitted group
  member, and check availability before joining and after leaving. Trade a
  finished item and verify the recipient can use it.
- 0.11 Moodles: save a status icon and preview in Recipe Creation. Use two copies
  and check that one timer refreshes. Test reconnect with time remaining, use
  without Moodles, and confirm that permit release clears only its effect.
- Seasons: configured administrator creates and activates while a draft is selected.
  Existing and newly verified players follow the active season automatically.
  Creation controls require seasons.create. Repeated refresh preserves assets.
- Player overview: membership rows show names only. Check the single reset on Home,
  the Type column, negative debt, and the red and blue badges on all four edges.
- Noticeboard: check notices with and without rewards or logos, long descriptions,
  group names that require a font fallback, the clear filter icon, and navigation
  to the correct group from its seal or signature.
- Windows: Home navigation on all four edges, tab switching from the launcher,
  resizing/scaling, writing readability, independent filters,
  focus and unsaved drafts. Click every launcher button twice, close its window
  using X, and verify its highlight. Trade closure cancels accepted exchanges but
  leaves pending invitations available. Compare Trade/Settings to Writing/Crafting
  and verify DPI sizing. Power toggle hides only automatic panels/HUDs
  while retaining individual preferences.
- Favorite Events: verify the optional header, hide-when-empty setting, and
  docked and floating layouts. Click a title and confirm Home → Events selects
  that event, shows its card first without duplication, and wraps its details at
  narrow widths. Check the event card’s logo, fallback initial, and group link.
- Recipe Creation: verify the separate tab with Alchemy-only, Cooking-only, and
  full recipe access. Start an editor draft, visit Crafting, then return and verify
  the draft survives. Check that Crafting shows production and Recipe Creation
  disappears when recipe permissions are revoked.
- Trade requests: send from the character menu or member list and check the compact
  popup, countdown, cancellation and expiry. Open the incoming request from the
  launcher badge. Check Decline, then send again and verify Accept opens the exchange
  for both players. Dismissing the request popup leaves its request available.
- Trade: fill an offer across several rows, filter inventory while an offer is
  saved, move partial stacks back, and compare both players' currency and item
  totals before and after final confirmation. Check item details in slot tooltips
  and confirm or cancel from the compact prompt.
- Interface: switch between Home tabs to check the gold page heading and verify
  the title bar controls. Expand and collapse
  Interface settings. Create a password-protected group, save a profile edit,
  switch activity tabs, and use the row star, name search, and Favorites only
  filter in Home → Groups. Open a group from a notice while filters are enabled.
- Inventory and Writing Log: check the Type column in both lists. Select and
  deselect an Inventory item, expand Use or Discard, and verify quantity limits
  and chat preview.
- Plugin updates: run an older build with the checker against a newer published
  version. Leave the quick bar idle and verify that no prompt opens. Try opening
  a feature from the bar, `/amorrp`, and Dalamud. Verify that gameplay remains
  blocked, the prompt opens Dalamud's update list, and the current build restores
  gameplay after installation. Simulate an unavailable feed and retry.

## Automated release checks

Build plugin/server with warnings-as-errors, locked dependencies and pinned Dalamud.
Run contract/helper tests and real disposable-PostgreSQL integration/upgrade tests.
Verify EF model parity, documentation/API-source parity, hostile media boundaries,
actual plugin ZIP contents, immutable feed publication, startup failure handling and
home update admission. Both exact-source workflows must pass before Windows bytes
are published. Current counts come from CI evidence, not this checklist.

## Operating readiness

Record deployed image/source, public readiness, real provider login, a tested isolated
backup restoration, bounded logs and measured capacity before claiming operational
readiness. Retention/export/deletion policy and destructive processing need explicit
design/authorization. No capacity guarantee or final 1.0 acceptance follows from CI.
