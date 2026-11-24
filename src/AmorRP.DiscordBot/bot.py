"""Discord gateway for Amor RP's server-authorized writing rewards."""
import asyncio
import hmac
import logging
import os
import re
import unicodedata

import aiohttp
import discord
from discord import app_commands
from discord.ext import tasks

LOG = logging.getLogger("amorrp.discord")
ROOT = "/api/v2/integrations/discord"
GROUP = app_commands.Group(name="amorrp", description="Amor RP writing rewards", guild_only=True)
WORD = re.compile(r"[^\W_]+")
CHANNEL_ID = re.compile(r"(?:<#(\d+)>|(\d+))")


class ApiError(Exception):
    pass


class WritingBot(discord.Client):
    def __init__(self, service_url: str, service_key: str):
        intents = discord.Intents.none()
        intents.guilds = True
        intents.guild_messages = True
        intents.message_content = True
        super().__init__(intents=intents, allowed_mentions=discord.AllowedMentions.none())
        self.tree = app_commands.CommandTree(self)
        self.tree.add_command(GROUP)
        self.service_url = service_url.rstrip("/")
        self.service_key = service_key
        self.api: aiohttp.ClientSession | None = None
        self.connected_guilds: set[int] = set()
        self.recent_authors: dict[tuple[int, int], float] = {}
        self.requests = asyncio.Semaphore(12)

    async def setup_hook(self):
        self.api = aiohttp.ClientSession(
            timeout=aiohttp.ClientTimeout(total=12),
            headers={"X-AmorRP-Discord-Key": self.service_key},
        )
        await self.tree.sync()
        self.reconcile.start()

    async def close(self):
        self.reconcile.cancel()
        if self.api is not None:
            await self.api.close()
        await super().close()

    async def call(self, method: str, path: str, data: dict | None = None) -> dict:
        assert self.api is not None
        try:
            async with self.requests, self.api.request(
                method, self.service_url + ROOT + path, json=data, allow_redirects=False
            ) as response:
                if 200 <= response.status < 300:
                    return await response.json() if response.content_type == "application/json" else {}
                if 400 <= response.status < 500 and response.content_type == "application/problem+json":
                    problem = await response.json()
                    raise ApiError(problem.get("title", "The request was declined."))
                raise ApiError("Amor RP is temporarily unavailable.")
        except (aiohttp.ClientError, asyncio.TimeoutError):
            raise ApiError("Amor RP is temporarily unavailable.") from None

    @tasks.loop(seconds=45)
    async def reconcile(self):
        try:
            guilds = await self.call("GET", "/guilds")
            self.connected_guilds = {int(value) for value in guilds["connected"]}
            present = {guild.id: guild for guild in self.guilds}
            for guild_id in (int(value) for value in guilds["leave"]):
                if guild_id in present:
                    await present[guild_id].leave()
                await self.call("DELETE", f"/guilds/{guild_id}")
            for guild_id in self.connected_guilds - present.keys():
                await self.call("DELETE", f"/guilds/{guild_id}")
                self.connected_guilds.discard(guild_id)
        except (ApiError, discord.HTTPException):
            LOG.warning("Discord server reconciliation failed. The bot will retry.")

    @reconcile.before_loop
    async def wait_for_discord(self):
        await self.wait_until_ready()

    async def on_guild_remove(self, guild: discord.Guild):
        if guild.id not in self.connected_guilds:
            return
        self.connected_guilds.discard(guild.id)
        try:
            await self.call("DELETE", f"/guilds/{guild.id}")
        except ApiError:
            LOG.warning("Could not report Discord server removal. The bot will retry.")

    async def on_message(self, message: discord.Message):
        if (message.guild is None or message.guild.id not in self.connected_guilds
                or message.author.bot or message.webhook_id is not None
                or not isinstance(message.author, discord.Member)
                or message.type not in (discord.MessageType.default, discord.MessageType.reply)):
            return
        writing = message.content.strip()
        if not writing or writing.startswith(("/", "!", ".")):
            return
        words = WORD.findall(unicodedata.normalize("NFKC", writing).casefold())
        if len(set(words)) < 2:
            return
        # Throttle bursts before requesting the server's authoritative cooldown check.
        now = asyncio.get_running_loop().time()
        key = (message.guild.id, message.author.id)
        if self.recent_authors.get(key, 0) > now:
            return
        if len(self.recent_authors) >= 4096:
            self.recent_authors = {author: until for author, until in self.recent_authors.items() if until > now}
            if len(self.recent_authors) >= 4096:
                return
        self.recent_authors[key] = now + 2
        normalized = " ".join(words)
        fingerprint = hmac.digest(self.service_key.encode(), normalized.encode(), "sha256").hex()
        try:
            await self.call("POST", "/posts", {
                "messageId": str(message.id),
                "guildId": str(message.guild.id),
                "channelId": str(message.channel.id),
                "userId": str(message.author.id),
                "roleIds": [str(role.id) for role in message.author.roles],
                "characters": len(normalized),
                "fingerprint": fingerprint,
                "createdAt": message.created_at.isoformat(),
            })
        except ApiError:
            LOG.warning("Could not submit Discord writing activity.")


def can_manage(interaction: discord.Interaction) -> bool:
    return interaction.guild is not None and interaction.permissions.manage_guild


async def redeem(interaction: discord.Interaction, code: str, route: str, success: str) -> bool:
    if interaction.guild_id is None:
        await interaction.response.send_message("Use this command in a Discord server.", ephemeral=True)
        return False
    await interaction.response.defer(ephemeral=True)
    try:
        await interaction.client.call("POST", route, {
            "code": code.strip(),
            "guildId": str(interaction.guild_id),
            "userId": str(interaction.user.id),
        })
    except ApiError as error:
        await interaction.followup.send(str(error), ephemeral=True)
        return False
    await interaction.followup.send(success, ephemeral=True)
    return True


@GROUP.command(name="connect", description="Connect this server using an Amor RP group owner's link code")
@app_commands.guild_only()
async def connect(interaction: discord.Interaction, code: str):
    if not can_manage(interaction):
        await interaction.response.send_message("You need Manage Server permission.", ephemeral=True)
        return
    if await redeem(interaction, code, "/guilds/redeem",
                    "Connected. Use /amorrp channels to choose eligible channels."):
        interaction.client.connected_guilds.add(interaction.guild_id)


@GROUP.command(name="link", description="Opt in and select your earning character using a code from Amor RP")
@app_commands.guild_only()
async def link(interaction: discord.Interaction, code: str):
    await redeem(interaction, code, "/players/redeem", "Discord writing rewards are linked to your selected character.")


@GROUP.command(name="channels", description="Set eligible channels and an optional required role")
@app_commands.guild_only()
@app_commands.describe(channels="Channel mentions separated by spaces", role="Limit rewards to members with this role")
async def channels(interaction: discord.Interaction, channels: str, role: discord.Role | None = None, paused: bool = False):
    if not can_manage(interaction):
        await interaction.response.send_message("You need Manage Server permission.", ephemeral=True)
        return
    ids = []
    for part in channels.split():
        match = CHANNEL_ID.fullmatch(part)
        channel = interaction.guild.get_channel(int(match[1] or match[2])) if match else None
        if not isinstance(channel, discord.TextChannel):
            await interaction.response.send_message("Choose text channels from this server.", ephemeral=True)
            return
        ids.append(str(channel.id))
    if not 1 <= len(ids) <= 25:
        await interaction.response.send_message("Choose between 1 and 25 text channels.", ephemeral=True)
        return
    await interaction.response.defer(ephemeral=True)
    try:
        await interaction.client.call("PUT", f"/guilds/{interaction.guild_id}/channels", {
            "channelIds": sorted(set(ids)),
            "requiredRoleId": str(role.id) if role else None,
            "paused": paused,
        })
    except ApiError as error:
        await interaction.followup.send(str(error), ephemeral=True)
        return
    await interaction.followup.send("Writing rewards paused." if paused else "Eligible channels saved.", ephemeral=True)


if __name__ == "__main__":
    service_key = os.environ["AMORRP_DISCORD_SERVICE_KEY"]
    if not 32 <= len(service_key) <= 256:
        raise SystemExit("Set an Amor RP Discord service key of at least 32 characters.")
    WritingBot(os.environ["AMORRP_API_URL"], service_key).run(os.environ["AMORRP_DISCORD_TOKEN"], root_logger=True)
