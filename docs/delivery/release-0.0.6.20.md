# Amor RP 0.0.6.20

## Player changes

- Favorite events now appear in a compact HUD widget, with no title bar, close button or resizing. Drag its header to place it in a corner; its position is remembered by Dalamud.
- The HUD lists ongoing events and events starting within the next five days, ordered by start time. Times use Server Time / UTC. Four rows fit at once; scroll for additional events and click an event for its full details.
- Hide or show the widget with Settings → General → Show favorite events HUD. Escape does not close it. It refreshes while the main menu is closed.
- Inventory, members, crafting data, policies, invitations, groups, notices/events and community images refresh separately. A failed or slow secondary request no longer prevents accepting successful updates in other features.
- A feature that cannot refresh retains its last confirmed information, shows an error in that section and retries. Its Details button includes the available error code and request ID. A rejected session still requires authentication.

## Deployment

Update the plugin through the existing main feed. Server 0.0.6.18 remains supported. This patch needs no server deployment or migration. Amor RP's API remains v2; XIVAuth's URLs, scopes, callbacks and server authentication are unchanged.

## Implementation and verification

The core community refresh now checks capabilities and loads the character's community list. Independent typed background reads own their completion, error, retry and cancellation state. Each result is applied on the UI thread. Character/community changes clear the corresponding readers; mutation and accepted-token generations reject stale responses. Favorites are scoped to the character, so changing the selected community does not clear the HUD. Hiding the HUD stops its polling.

The rolling five-day filter includes ongoing events and the exact five-day boundary, excludes finished events, and updates as time passes without waiting for another request. Calendar labels use UTC even when a timestamp carries another offset.

Local verification: 115 helper tests pass, including the group-editor regressions and 11 new refresh/timing cases. The plugin builds with zero warnings/errors. CI also checks the server, contracts, Windows package and distribution invariants before publishing.

Manual in-game checks remain necessary for rendering and placement: drag the HUD into a corner, close the main menu, inspect/scroll events, press Escape and confirm only Settings hides the HUD. No live game client is available in the build environment.
