# Plugin interface

Current behavior is implemented in feature-owned screens and the shell; see the
[source map](../engineering/repository-layout.md). Native rendering/scaling and
multi-character interaction still require [in-game acceptance](../delivery/acceptance.md).

| Window | Entry and purpose |
| --- | --- |
| Settings | `/amorrp` or `/amorrp settings`; Interface, Account, About |
| Home | Icon tabs for season overview/directory/noticeboard, Inventory, Crafting, and the Writing Log. Commands `/amorrp season`, `/amorrp inventory`, `/amorrp crafting`, and `/amorrp writing` select their respective tabs. |
| Group management | `/amorrp groups`; owned/authorized group settings and staff features |
| Season management | `/amorrp manage`; authorized profile, permissions, economy and administration |
| Trade | `/amorrp trade`, member action or character context menu; requests and exchange |

Home, Inventory, Crafting and Writing shortcuts open the shared window on the
corresponding tab. Press the selected shortcut again to close it. Other window
buttons keep their own toggles. Context actions open their requested content.
All launcher icons, including Power, use the same icon-button size and renderer.
Choose the Home navigation edge under Settings → Interface → Main window.

Launcher order is Home, Inventory, Crafting, Writing log, Group Management, Trade, Season Management and Power. Group and admin controls follow their permission checks. Power toggles automatic HUDs and remembers the favorite-events preference.

## Docking the quick bar

In Settings → Interface, choose Top or Bottom for a horizontal bar, or Left or
Right for a vertical bar. Use Position to move it along the edge, or drag the
handle itself. Size and Opacity apply immediately.

With Auto-hide on, hover over the small edge handle to reveal the buttons. Click
the handle to pin the bar open for the session, or click again to let it retract.
Turn off Auto-hide to keep the bar visible across sessions. Clear Dock to edge
in Settings or choose Undock from the handle menu to drag the bar freely.
Favorite Events lists only upcoming event titles. Select a title to see its
details. Its settings control docking, width, opacity, background opacity, header
visibility and whether to hide the panel when empty.

## Shared season context

Every verified character automatically receives every non-deleted season. The
selector browses them; it does not join or leave them. Default to the active season
on login/rollover and preserve deliberate archive browsing across normal refreshes.
Show draft/archive read-only state. Do not display obsolete join/create prompts.

Only `seasons.create` holders see creation controls. Administrators can activate or
archive with typed-name confirmation and grant creation permission under Creation
access, with a reason. Normal players never see these controls. Season owners and
delegated staff see only their relevant scoped management tools. No season
membership removal/block/restore or inactive-member controls remain.

## Data, drafts and feedback

Inventory supports tabs, categories, search, sorting, item details, pins and bounded
pagination. Finances appear at the bottom of Inventory. Its filters/selection
are independent of the Writing reader. Future Supplies
and Documents entries are honest empty states rather than unimplemented actions.

Drafts survive polling. Switching member/group context protects unfinished edits.
Permission updates reload after confirmed writes and keep conflict/recovery details
available. Background reads do not reorder or overwrite active editing unnecessarily.

The note icon opens Activity Log with recent results, detail, and pending-action recovery. The cog opens Settings.
Keep the original operation key and request for recovery. Checking/retrying a saved
operation never repeats game chat. Do not reinterpret an unknown old command as a
new action. Authentication mismatch blocks feature use while preserving assets.

Account uses verified XIVAuth browser pairing. Interface preferences work while
signed out. About reports installed/last-connected versions without credentials.
The release endpoint is fixed in ProductServer; players do not edit hosting URLs.

## Feature details

Writing reads only currently owned private bodies. Creation/edits use bounded
wrapped text without adding soft-wrap characters to stored content. In an accepted
trade, move items between the Amor RP inventory and your offer grids by dragging
or right-clicking. Pick a quantity for stacks. Your partner's grid is read-only.
Both approval stages are revision-bound, and closing the exchange cancels it.
Billing/treasury controls require their approved group permissions. Manage Groups
never exposes group-private content. Financial success/failure leaves a durable
receipt and accurate available/reserved balances.

## Shared theme

PluginTheme applies one dark-and-gold palette and scaled rounding around the plugin
WindowSystem. Settings, Trade, Writing, workspaces, Inventory and popups
inherit that same style. Individual windows retain placement/behavior; the theme
stack restores after drawing and does not restyle other plugins.
