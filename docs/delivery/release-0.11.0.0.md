# Amor RP 0.11.0.0

This server release includes class selection and resets (0.9), group recipes
(0.10), and server support for optional Moodles (0.11).

- Classes grant Alchemy, Cooking, or Gathering and Hunting alongside manual
  permissions. The owner sets the number of slots and the reset potion price.
  Applying a paid reset preserves weekly point usage and bonuses.
- Group recipes require group membership and the relevant discipline permissions.
  Finished items can still be traded and retain their original recipe revision.
- Identical saved consumable effects refresh one timer. Permit debuffs follow
  registration and release. The plugin applies these as optional display effects.

Use the home update script to build verified main, then select the printed image
tag in Portainer. Keep the existing database volume and settings. The server
applies the schema migration at startup. Check readiness and the advertised
server version before installing the matching plugin. [Home server guide](home-server.md)
