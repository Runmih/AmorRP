# Discord writing rewards on the home server

The bot runs beside the Amor RP API as a separate container. It has no database
access. It relays eligible new Discord posts to the API, which checks linking
and rewards, credits the character, and collects eligible debt.

## Set up the Discord application

Create a Discord bot application, enable **Message Content Intent**, and invite
it with the bot and application command scopes. Give it permission to view the
writing channels you plan to enable. A Discord member with **Manage Server**
permission connects the server and selects channels. Enable the privileged
intent through Discord’s approval process if your application requires it.

## Add the container

1. Upgrade the Amor RP server and game plugin to 0.16.0.0.
2. From the AmorRP checkout, build the bot image on the home server.

   ```bash
   docker build -f deploy/discord.Dockerfile -t amorrp-discord:0.16.0.0 .
   ```

3. Add `deploy/home/compose.discord.yaml` to the existing stack as a Compose
   override. If using the Portainer web editor, merge its service, API
   environment and network entries into the current stack. Keep the existing
   API networks and all existing environment values.
4. Add `AMORRP_DISCORD_IMAGE=amorrp-discord:0.16.0.0`,
   `AMORRP_DISCORD_TOKEN` from the Discord Developer Portal, and
   `AMORRP_DISCORD_SERVICE_KEY` as a new random private value of at least 32
   characters. Keep the token and service key in Portainer’s private environment
   values.
5. Deploy the stack. The bot registers the `/amorrp` commands after connecting.

## Connect and use

1. The season owner enables rewards in **Season Management → Economy → Discord
   rewards** and grants the group **Discord Bot Permission** in **Group access**.
2. The group owner opens **Home → Groups → Discord**, creates a link code and
   gives it to the Discord administrator if they are different people. The
   administrator redeems it with `/amorrp connect` and picks writing channels
   with `/amorrp channels`. The command takes a list of channel mentions and an
   optional required role.
3. A player opens **Home → Character → Discord writing rewards**, creates their
   link code, and runs `/amorrp link` on that server. To switch characters,
   repeat from the intended character. **Unlink Discord** opts the character
   out. Account and character limits survive relinking.

A group can connect one Discord server. Disbanding, withdrawing Discord Bot
Permission, disconnecting the link or archiving the season queues bot removal.
The bot reconciles removals after reconnecting. There is no backfill for activity
missed while the bot was offline. The reward ledger records message identifiers
and a keyed fingerprint for repeat detection. It does not store message bodies.
