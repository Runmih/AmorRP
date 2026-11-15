# Amor RP 0.4.0.1 — focused settings window

The former main menu now opens **Settings**. Its feature sidebar and embedded
inventory, crafting, groups, members, noticeboard and management pages are removed
from the window. Those features stay in their dedicated windows and launcher entries.
The writing book and closed-eye controls retain their existing behavior; no extra
launcher button is added for Settings.

Settings has three tabs:

- **Interface:** quick launcher visibility, the master attached-panel switch, the
  inventory companion preference and the favorite-events HUD preference. Choices
  save immediately; hiding all attached panels retains the individual preferences.
- **Account:** the existing verified XIVAuth sign-in, browser pairing and device
  sign-out controls. Trade's sign-in prompt opens this tab directly.
- **About:** installed plugin version, the last connected server version and a
  button to copy those versions for troubleshooting, plus existing window commands.
  It never copies account credentials or pending requests.

`/amorrp`, `/amorrp settings`, Dalamud's main UI action and the plugin configuration
action open Settings. The title and installer command hint identify it accordingly.
Local preferences are available while signed out or before logging into the game.
Feature windows retain their verified-character guards. Settings contains no season
selector or season/group administration forms; trade setup and notification/group
navigation route to the appropriate dedicated window.

Pending-operation recovery and connection feedback remain available in the shared
footer. Existing sessions, window identity, panel preferences and saved data are
preserved. The smaller default window remains resizable.

This is a plugin-only patch. Server source stays 0.4.0.0; the plugin also supports
the existing 0.3.0.0 server in community mode. There is no database migration or
configuration migration. Dalamud API 15, Amor RP API v2 and XIVAuth API v1 remain.

Verification requires the API 15 plugin build, package inspection, existing helper
tests, documentation checks and both CI workflows for the published source. Game
checks: open Settings signed out; toggle/save all Interface preferences; sign in and
out from Account; check/copy About versions; open the dedicated feature windows;
and check trade's signed-out link and normal pending-action recovery.

The stable feed publishes 0.4.0.1 from source `7dc704e`, using the exact
[successful Windows artifact](https://github.com/Runmih/AmorRP/actions/runs/43738230571).
All 195 helper tests, package/documentation checks and the matching
[server/contracts workflow](https://github.com/Runmih/AmorRP/actions/runs/43738230655)
passed. In-game layout and interaction acceptance remains separate.
