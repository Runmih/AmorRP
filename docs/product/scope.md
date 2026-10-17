# 1.0 product scope

## Purpose and successful session

Amor RP gives a character a fictional inventory and currency inside closed RP
communities. A community's owner controls supply and permissions. Two players can enter
the same community, create or obtain a potion and a letter, trade items/currency,
read the letter, and use the potion with a visible, consented in-game message.
Those holdings survive plugin reloads and server deployments.

No item changes FFXIV stats, combat, real inventory, gil, or game permissions.
RP value is fictional; 1.0 has no real-money exchange or gambling functionality.

## Required features

| ID | Feature | Required result |
| --- | --- | --- |
| F01 | Login | XIVAuth verifies selected character; server issues revocable character-bound session. |
| F02 | Communities | Create, invite, join, leave, remove, block/unblock member, transfer ownership, delete community; enforce limits. |
| F03 | Switching | Persistent community dropdown above tabs, shared refresh/status; every screen shows community and character. |
| F04 | Permissions | Owner grants moderator actions; authorized gameplay managers grant Trading/Craft Potions. General Moderation edits profile text/currency name only. |
| F05 | Currency | Named currency with owner-uploaded PNG/JPEG/WebP icon up to 128x128; display balance; authorized issue/remove; trade currency; inspect relevant history. |
| F06 | Categories | Seed Consumables and Books; preserve existing categories without a category-creation workflow. |
| F07 | Potion definitions | Owner or explicitly authorized recipe editor sets name, category, description, message, creation cost, availability. |
| F08 | Potion production | Authorized character spends its own weekly points to create quantities. |
| F09 | Books writing | Book, letter and note subtypes share a weekly quota; read owned text, edit eligible untraded text. |
| F10 | Inventory | Extended searchable/filterable list and type-specific details; compact pinned-potion view. |
| F11 | Potion use | A deliberate Use action consumes one and posts the exact preview to the selected channel; no chat permission window or checkbox. |
| F12 | Trading | Character context menu opens community-bound trade; quantities/currency, mutual confirmation, cancellation. |
| F13 | History | Personal operations and owner administrative audit; no letter bodies in logs. |
| F14 | Recovery | Reconnect, recover unknown request outcomes, preserve data and reservations across restarts. |
| F15 | Settings | Character/community-specific pins and channel, saved login/devices, release-managed endpoint, display preferences. |
| F16 | Service lifecycle | Version compatibility, maintenance messaging, export/deletion request, backups and support. |
| F17 | Community Home and announcements | Responsive profile/logo overview with inline authorized editing and publishing; five retained announcement messages with persistent character read state and Home/launcher badges; owner Community Settings and per-type weekly policy tabs. |

Community CRUD and recovery are necessary supporting features, not additional RP
systems. Contextual owner controls ship in the plugin; a separate administration website is
not required. Browser interaction is limited to authentication and documentation.

## Deferred beyond 1.0

Additional behaviors (weapons, equipment, rich books/chapters, containers), crafting ingredients,
recipes with prerequisites, shops/markets, auctions, community banks, multiple
currencies, currency exchange, character stats, combat automation, shared account
allowances, named roles/presets, public directories, general image uploads and attachments (currency icons are the 1.0 exception),
full localization, mobile/web inventory editing, multiple server instances, and
offline writes. See [expansion](expansion.md).

## Release meaning

"Working 1.0" means the complete session above, with tested isolation, quota and
trade integrity, clean installation, a documented operating budget, recovery,
and a human able to maintain it. It does not promise unlimited capacity or
official plugin-repository acceptance. A closed alpha may precede that standard.
