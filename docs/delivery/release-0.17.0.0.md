# Amor RP 0.17.0.0

- Home → Character shows ability scores, permanent stat point allocation, equipment
  slots, HP, and icon-based actions. Choose a season member for a single-target attack
  or roll a stat check. Select a chat channel or keep rolls in the Activity Log.
- Season Management → Gameplay → Combat sets the stat allowance, weekly Blacksmithing
  allowance and Downed Moodle. Authorized staff can set or restore HP for a member.
  Season staff can revoke a member’s attack permission.
- Blacksmithing recipes create weapons and armor. Weapon recipes choose a sword or
  staff, STR or DEX, and Power. Armor recipes set AC. Gear uses its saved recipe
  revision and must be available in its owner’s inventory to contribute.
- Alchemy potion recipes can add a permanent stat point grant under Extra effects.
  The grant follows the crafted revision and only takes effect on consumption.
- The built-in Character Reset Potion is sold under Shop → Products. Owners adjust
  the price and purchases per character per season under Manage products. Buy it,
  then use it in Home → Character to refund allocated stats and clear class choices.
  Permanent earned points, spent weekly resources, equipment and HP are preserved.
- Settings → Interface → Health controls the dockable HP HUD. This milestone covers
  physical attacks and stat checks. Spells and Mana remain future work.

Ability scores start at 10 and cap at 20. Each character starts at 20 HP.
With an equipped weapon, roll a d20 plus the weapon’s ability modifier and
+2 proficiency. Natural 1 misses and natural 20 critically hits. On a hit,
deal weapon Power plus the ability modifier, with a minimum of 1 damage.
A critical hit doubles weapon Power before adding the modifier. Unarmored AC
is 10 plus the DEX modifier. At 0 HP the character is Downed until staff restore HP.

Deploy the matching server and database migration before installing this plugin.
