# Amor RP 0.3.0.3 — quick panel visibility switch

This plugin-only patch replaces the quick launcher's community dropdown with a
single Hide panels / Show panels button. The eye icon and button text make the
current action visible without a tooltip. The enabled state has a gold background.
The launcher retains the main-menu and trade buttons and their notification badges.

The switch hides the inventory companion and favorite-events HUD. It stays visible
so the panels can be restored with one click. Manually opened main, trade, compact
consumables and community-form windows remain usable, and trades are not cancelled.
Community selection remains in the main menu and the inventory companion.

Visibility is saved through Configuration.ShowAttachedInterfaces, defaulting on for
existing settings. The individual ShowInventoryCompanion and ShowFavoriteEvents
preferences are preserved: restoring panels never enables one the user had disabled.
Future automatic panels should combine this master flag with their own preference
and normal visibility conditions. The switch does not sign out, change community,
disable notifications or change any accounting/permissions.

Settings → General → Show attached panels and HUDs and `/amorrp panels` control the
same saved state, including when the quick launcher itself has been disabled.
Favorite-event reads pause while hidden and refresh on restoration; inventory's
existing visibility lifecycle refreshes when its attached panel is restored.

Server and minimum client stay 0.3.0.0; no backend update, migration or new dependency.
The release uses the normal main feed and an unchanged successful Windows artifact,
with both Plugin package and Server and contracts checks required for its source.
Package provenance is recorded beside the versioned ZIP.

## In-game checks

1. With vanilla inventory open and the events HUD enabled, click Hide panels. Both
   disappear and the launcher remains available with Show panels. Click it again;
   inventory returns on the right and the HUD returns in its previous position.
2. Disable one panel in Settings, toggle off/on, and confirm that panel stays disabled.
3. Hide panels, reload the plugin and verify the hidden state persists. Restore from
   the launcher, Settings, and `/amorrp panels` and check they remain synchronized.
4. Verify main-menu/trade buttons, notification badges and community selection in
   the main menu/companion. Hiding panels must not cancel or modify an active trade.
5. Switch characters and open/close inventory while panels are hidden. No attached
   panel should reappear until the master switch is enabled.
