# Amor RP 0.8.0.0

This release includes the Season Shop from 0.6, ownership permits from 0.7,
and Gathering and Hunting from 0.8.

- Open **Shop** between **Home** and **Inventory**. Season owners list recipe items,
  choose who can buy them, and set optional stock and per-buyer limits. Purchases
  credit the season treasury and deliver items to the buyer's inventory.
- Under **Shop → Slave**, review listing invitations, registration offers, and
  named permits. Moderators with the listing permission can nominate a character.
  The right-click **Enslave** action starts a private offer for a group owner.
  The addressed player must accept the exact quote before the group can register.
- **Crafting → Gathering and Hunting** uses one weekly budget to create tradeable
  Alchemy Supplies and Cooking Supplies. Use the matching pack from Inventory for
  its full point bonus, including points above the base weekly allowance. Unused
  bonus points expire at the crafting reset.
- Season owners configure supply costs, bonuses and Gathering points under
  **Season Management → Weekly limits**.

Update the home server to 0.8.0.0 and verify its migrations and readiness before
publishing the plugin to the installer feed. The 0.8 plugin requires server 0.8.0.0.
All three migrations apply automatically in order. Keep the existing database
volume and settings. [Home server guide](home-server.md)
