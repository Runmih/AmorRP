# Amor RP 0.3.0.4 — writing log and icon-only panel toggle

The launcher's existing book button now opens a dedicated, movable/resizable writing
log. Its main-menu hookup and announcement badge are removed. No replacement
main-menu button is added. The inventory companion's generic Main menu shortcut is
also removed; its community selector now uses the full header width. Existing
commands and task-specific actions such as View bills retain their behavior.

The launcher has three equal-sized buttons: writing log, trading and a custom drawn
closed eye. The eye has no text or tooltip. Its gold enabled background and neutral
hidden background retain the shared panel visibility state from 0.3.0.3.

## Writing log

- A searchable Books / Letters / Notes library sits beside a large, scrollable,
  selectable-text reader. It has its own filters, sort order, pagination and selection.
  Lists contain only inventory summaries; private bodies are fetched only for the
  selected written item. Readers can copy the text explicitly.
- A community selector scopes the library. Inventory's Read in writing log action
  opens the exact owned item by holding ID, including items beyond the first page.
  Small inventory panes no longer embed the full writing reader/editor.
- New writing uses the existing book/letter/note creation rules and weekly allowance.
  Authors can edit only their own unsealed, unreserved writing. Saved-version checks,
  draft preservation during refresh and existing command recovery remain in use.
  Title/body limits and server permissions are unchanged.
- The reader takes the remaining pane height. The window defaults to 1000×740,
  resizes, and stacks the library above the reader in narrow layouts. Creation and
  editing also use the available height instead of a small fixed reading box.
- The log is manually opened, so the panel visibility toggle leaves it usable.
  `/amorrp writing` opens it directly. Closing/reopening preserves local drafts;
  changing character or community clears private views and drafts.

The log shares existing login, generation guards and command recovery, not a second
session. Written responses are checked against the expected community, current
holder, selected holding and matching written-item identity. Missing ownership
clears the reader. Inventory mutations/trade completion require a fresh owned read
before a previously displayed body is shown again.

## Verification and release

The plugin builds against pinned Dalamud API 15 with no warnings/errors. All 163
plugin/helper tests pass, including writing pagination/filtering, selected-body
reads without list-body prefetch, other authors' legitimately owned items, rejection
of mismatched identity/type responses and clearing a removed selection.

The main feed receives the unchanged Windows artifact only after both Plugin package
and Server and contracts checks pass for its exact source. Provenance beside the
ZIP records that source, build and digest. Server/minimum client remain 0.3.0.0;
no backend deployment, migration or dependency change is required.

## In-game checks

1. Click the launcher's book: the writing log opens without the old main menu.
   Check the closed-eye button has no label, still toggles panels, and never hides itself.
2. In the log, switch communities, search/filter titles, sort and load more pages.
   Read each writing type, resize the window, select/copy text, and test Unicode and
   legacy stored bodies longer than current creation limits.
3. Open writing from inventory and confirm the exact item's title/body/author. Close
   inventory and continue reading in the independent log.
4. Create each writing type, edit an owned original, reject editing a traded/sealed
   or reserved item, and verify quota and error feedback. Refresh while drafting;
   text must not be overwritten. Test the existing same-action recovery path.
5. Trade/discard the selected text, lose community access, switch characters and
   log out. Private text must clear and late results must not restore an old scope.
6. Verify the trade launcher, red trade badge, saved panel state and existing commands.
   Announcements remain available in the existing community interface.
