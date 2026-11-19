# Product rules

These rules describe implemented behavior. Future features belong in
[planning](../planning/README.md); [decisions](../decisions.md) records maintainer direction.

## Identity, seasons and authority

Provider verification binds a stable character ID to ownership. Name and home world
are mutable display fields; renames/transfers never create or merge inventories.
Alts keep independent budgets. Public IDs and client hints do not prove ownership.

Every verified character belongs to every non-deleted season, including drafts and
archives. There are no season invitations, joining, leaving, removal or membership
blocks. User bans belong to separate account moderation. The migration restores
old inactive/blocked membership rows without reviving old grants or resetting assets.

Creating a draft requires global `seasons.create`. Privately configured Lodestone
administrators receive it automatically and can grant/revoke it with a reason.
Only administrators activate/archive seasons. One season is active. New seasons
start fresh; existing assets remain in their original economy. Drafts and archives
reject fresh gameplay mutations. Durable retries may recover their original receipt.

Season ownership is scoped authority, not global creation permission. Ownership
transfer is proposed by the owner and accepted by the recipient; it transfers
existing scoped authority and does not copy assets or grant global administration.

## Permissions and RP groups

Server checks use current database state, never long-lived token capability claims.
Owners have implicit scoped authority. General moderation (`season.moderate`),
announcement publishing, gameplay-grant management, currency adjustment, recipe
definition, crafting, item removal, group creation and group management are separate
actions. Staff may not escalate their own authority. Hide unauthorized UI controls.

RP groups live inside a season. Their open/closed/password joining rules and staff
permissions are independent of automatic season membership. A character may own
three groups per season; archives do not consume a new season's group allowance.
The season owner approves group features. The group owner delegates staff grants
within those approved features. Season moderation/deletion rights do not expose
private group content. Leaving a group clears its old staff grants.

## Currency, treasuries and billing

Money uses checked whole-unit integers internally and decimal strings on the wire.
A season has one currency identity and owner-managed name/icon. Available funds
exclude reservations and collection holds. No float calculations or client balances
are authoritative. Adjustments, transfers and settlement preserve ledger invariants.

Approved groups have a treasury. Deposits/withdrawals and receipts are atomic and
replay-safe. Disband returns funds to the actual group owner, never the deleting
moderator. Overflow remains claimable by that owner. Group item storage is planned.

Trusted authorizes a group to bill a character, including offline. Billing staff
need the appropriate approved feature/grant. A durable bill ID prevents repeat
charges; a short cooldown also reduces accidental fresh charges. New debt is allowed
only under the season's debt policy. Existing debt remains payable when new debt
is disabled. Unpaid debt is a claim, not newly created spendable money.

Oldest unpaid bills collect available unreserved funds and subsequent income.
Collection retains trade reservations and continues durably for long queues. All
income paths use settlement; collection cycles terminate without minting money.
Disbanded-group claims persist and later payments go to the former owner through
Treasury returns. Taxes/commission and permit billing locks are not implemented.

## Inventory, recipes, quotas and writing

Categories organize season-owned types; they never grant permission. Recipes have
immutable revisions. Creation checks current revision, category/type policy,
permission, quantity and weekly cost inside its transaction. Retirement preserves
existing copies. Upgrades do not silently broaden old categories or policies.

Quotas belong to a character/season/week. Alchemy and Cooking budgets are separate;
book/letter/note share the writing allowance. Monday 00:00 UTC is the reset, with
no rollover. Policy edits apply next period. Grant changes, refreshes, sign-in and
season selection do not refill spent budgets. All production is bounded and atomic.

Holdings retain stable IDs, type and provenance. Reservations reduce availability.
Only a holder reads a private written body; moderator inspection is limited to the
summaries needed for authorized item removal. Eligible author-held untraded writing
can be edited; traded writing locks its content. Private bodies never enter logs,
public history or unrelated players' receipts. Text is bounded plain text.

Current bounds are reported by capabilities: profile/recipe descriptions use 300
text elements, written bodies 500, writing titles 80 and chat messages 50. Use the
shared validation limits instead of adding per-screen interpretations.

## Consumption, chat and trading

An explicit Use previews the exact validated message/channel. The server commits
one item use before the plugin posts to game chat. No startup chat-consent dialog
or checkbox exists. Literal `<3` stays literal. Reject commands, control characters,
unsupported placeholders and invalid destinations. A lost response or chat failure
never triggers automatic reposting or a second item mutation.

Trade invitations reserve nothing. Accepted offers reserve available items/currency.
Both players approve the current revision, then both approve final review. Changes
invalidate approvals; stale revisions cannot complete. Exchange commits atomically,
including currencies, items and settlement. Cancel/expiry releases reservations.
Disconnect, restart or a delayed worker never makes stale reservations authoritative.

## Profiles, notices and UI

Character descriptions are self-edited and season-scoped. Personal notes remain
private local protected data. Season/group profiles and normalized logos have
separate authorization and image bounds. Latest five season announcements have
persistent per-character read markers. Group-private announcements require group
membership. Notices/events and favorite-event HUD data stay season/group scoped.

Settings contains Interface/Account/About. Dedicated feature windows own their
layout and drafts. The inventory companion attaches to the right of native inventory;
Writing provides a full reader. The launcher book opens Writing. The icon-only
power switch hides automatic panels/HUDs and remembers individual preferences.
No old main menu, community selector or season join/leave controls are exposed.
