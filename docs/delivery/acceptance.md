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
- Seasons: configured administrator creates and activates while a draft is selected.
  Existing and newly verified players follow the active season automatically.
  Creation controls require seasons.create. Repeated refresh preserves assets.
- Player overview: membership rows show names only. Check the single reset on Home,
  the Type column, negative debt, and the red and blue badges on all four edges.
- Windows: Home navigation on all four edges, tab switching from the launcher,
  resizing/scaling, writing readability, independent filters,
  focus and unsaved drafts. Click every launcher button twice, close its window
  using X, and verify its highlight. Trade closure cancels accepted exchanges but
  leaves pending invitations available. Compare Trade/Settings to Writing/Crafting
  and verify DPI sizing. Power toggle hides only automatic panels/HUDs
  while retaining individual preferences.
- Favorite Events: only titles in the panel, optional header, click for details,
  and hide when empty. Verify docked auto-hide and floating placement.
- Trade: fill an offer across several rows, filter inventory while an offer is
  saved, move partial stacks back, and compare both players' currency and item
  totals before and after final confirmation. Check item details in slot tooltips
  and confirm or cancel from the compact prompt.
- Interface: switch between Home tabs to check the gold page heading and verify
  the title bar controls. Expand and collapse
  Interface settings. Create a password-protected group, save a profile edit,
  switch activity tabs, and favorite the group from Home → Groups.
- Plugin updates: run an older build with the checker against a newer published
  version. Verify that Amor RP windows and trade context actions are blocked,
  that the notice opens Dalamud's update list, and that the current build
  restores gameplay after installation. Simulate an unavailable feed and retry.

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
