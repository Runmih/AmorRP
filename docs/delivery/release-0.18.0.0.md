# Amor RP 0.18.0.0

Update the home server and let its database migrations finish before switching
players to the 0.18 plugin. Both components require version 0.18.0.0 or newer.

- Character stats use 5e point buy. Constitution modifies maximum HP. Weapons
  roll damage dice and use their selected ability for accuracy and damage.
- Create reusable abilities under Season Management → Skills & Spells. The
  Classes tab assigns abilities, crafting permissions, weekly bonuses, and the
  optional Stay Standing trait. Granted abilities appear on the Actions HUD.
- Create timed modifiers and their optional Moodle in Season Management →
  Status Effects. Consumable recipes and successful skills can apply them.
- Blacksmithing points are managed in Gameplay → Weekly limits.

The database migrations preserve purchased ability scores, existing equipment and
potion revisions, and active Moodle timers. This release does not reset data.
