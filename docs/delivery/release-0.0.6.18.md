# Amor RP 0.0.6.18

## Player changes

- Crafting now has Alchemy and Alchemy recipes, Cooking and Cooking recipes, and Writing.
- Alchemy supports potions, poison, explosives, drugs and medicine. Cooking supports meals, drinks and desserts.
- Separate Craft Cooking Items, Create Alchemy Recipes and Create Cooking Recipes permissions. Cooking has its own weekly points, scheduled from Weekly Policy; existing Alchemy permissions and points are retained.
- Group creators own their groups. Create Groups remains a community-granted permission. A character can own three active groups across communities; joining groups has no membership cap.
- Only the group owner edits its profile, logo, joining conditions and content. Manage Groups allows deletion only. It gives no access to private members or announcements, including for community owners.
- Groups support Open, Closed and Password protected joining. Group member lists are paginated; password joining uses a separate dialog.
- Each group owner can publish one community-visible request on the Noticeboard, with an optional descriptive reward. Search filters titles, group names, requests and rewards. No currency is automatically moved or reserved.
- Groups can schedule multiple events using Server Time / UTC, with a title (40 characters), description (100), location, start and end. Up to 20 upcoming events per group protects retained storage; past events are removed when scheduling new ones.
- Favorite groups to follow their events across your joined communities in a separate persistent window. Toggle it under Settings → General. Finished events disappear automatically.
- Private group announcements retain the latest five and are visible only to group members.

## Deployment and migration

Deploy main on the existing Render service, then update the plugin from the unchanged custom repository URL. Both plugin and server are 0.0.6.18; the advertised minimum plugin version is 0.0.6.18. No additional Render service, database, OAuth application or callback change is needed.

The migration retains inventories, balances, existing quotas, community data and group profiles. Existing profiles have no stored creator, so their community owner becomes their initial owner and member. Existing custom categories are left untouched; separate Alchemy and Cooking categories are added for existing communities. Existing communities start with zero Cooking points; owners schedule their desired weekly budget. New communities can set an initial Cooking budget in the creation form. Group passwords are stored as salted hashes and never returned to clients.

Downgrading the database is refused when new group activity or new crafting data would be lost. Authentication and XIVAuth endpoint addresses are unchanged.
