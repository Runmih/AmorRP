# Amor RP 0.3.0.2 — inventory companion (candidate)

This plugin-only candidate adds a dedicated RP inventory panel to the right of the
game's inventory. The published feed remains 0.3.0.1 until the verified Windows
artifact is promoted through the existing publication workflow. Server and minimum
client remain 0.3.0.0; no backend deployment, migration or hosting change is required.

## Interface

- Opening vanilla inventory automatically shows the companion. It tracks the native
  window's right edge and scaled height, and hides when inventory closes. Standard,
  expanded and large layouts are looked up independently each frame. No native UI
  nodes are modified or retained.
- Dark surfaces and gold accents separate the compact community selector, item
  browser and fixed personal-finance footer. There are no management forms here.
- All, Consumables and Writing tabs use real inventory data. Supplies and Documents
  are described empty places for the planned packages and registered permits;
  they issue no unsupported requests and contain no simulated assets.
- Search, type filtering and name/type/category sorting happen on the server before
  pagination. Filters, loaded pages and item selection are independent of the main
  inventory view. Selecting an item opens its details below the list, reusing the
  existing use/read/edit/pin/discard actions and command recovery.
- The bottom shows personal available currency, total coins, trade reservations,
  outstanding debt and any coins held for repayment. These use backend totals rather
  than summing the currently loaded page. Unknown debt is shown as a dash; failed
  debt refresh is identified. View bills opens Home in the main menu.
- Settings → General → Show RP inventory beside game inventory toggles the panel.
  Existing installations enable it by default without changing their saved identity.

The attachment stays on the right. It narrows to the remaining viewport space;
when that is too narrow for the browser it asks the player to move inventory left.
With less than 100 native-scaled pixels available it hides, keeping the main menu
accessible through the launcher or `/amorrp`. Short layouts scroll internally.
Font size follows Dalamud's UI settings while the outer geometry follows game scaling.

## Verification and publication

The candidate builds against the pinned API 15 reference with no warnings/errors.
All 149 plugin/helper tests pass. New checks cover right-side placement,
scale/viewport changes, limited space, invalid geometry, family filters, escaped
search, unsupported-family refusal and whole-unit amounts beyond an individual
wallet's range. These do not render native game windows or prove live placement.

Before feed publication, run the existing Windows package and server/contracts
workflows for the final source and publish the unchanged Windows artifact with its
provenance. Do not overwrite the published 0.3.0.1 package. A locally built candidate
is not evidence of successful Windows CI or in-game acceptance.

## In-game acceptance

1. Open and close vanilla inventory repeatedly, including with inventory already open
   when the plugin loads. The companion appears on the right and never steals focus.
2. Move inventory, change its game UI scale, change resolution/Dalamud font size and
   switch between standard/expanded/large layouts. Verify alignment, scrolling,
   right-edge behavior and the explanation when there is insufficient room.
3. Hide the game UI and switch characters/log out. The panel hides, and no former
   character's items, written contents or balances appear after the switch.
4. In a test community, search, filter, sort and load multiple pages. Select and use
   a consumable, read/edit eligible writing and discard a test item. Reserved copies
   remain protected. Keep the main inventory open too and verify its independent
   filters and discard confirmation.
5. Compare the finance footer with authoritative balances and Home bills before and
   after a trade, bill and debt repayment. Check unavailable-network feedback and
   saved-action recovery; no timed-out action is resubmitted with a fresh identity.
6. Toggle the attachment setting. Check the existing launcher, main menu, trade,
   community selector and compact consumables still work.

The four billing/debt acceptance checks from [0.3.0.0](release-0.3.0.0.md) remain
pending. This interface candidate does not mark S03 or later features accepted.
