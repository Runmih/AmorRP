# Product rules

Rules below combine agreed scope and recommendations from the
[decision register](../decisions.md). They define the proposed 1.0 behavior.

## Identity and isolation

Inventory ownership, capability grants, quota usage, and trade restrictions belong
to a verified **character within a community**. Different characters can receive
independent budgets, even when controlled by the same person. Do not correlate
alts or silently share their quotas.

Display name plus home world identifies a visible character for context-menu
matching. Authentication uses provider-verified identity, never a client assertion
alone. Store a verified stable character key; names/worlds are mutable display
fields. Provider identity migration must be explicit. No inventory merging by name.

Rename and home-world transfer preserve the same internal Character ID, inventory,
currency, memberships, permissions, restrictions and quota history. Verified
provider profile updates change display fields only. Existing assets remain intact
while a stale provider profile or fresh verification is resolved; never create a
new inventory merely because the old name/world no longer matches.

Every item, definition, category, currency record, reservation, and operation has
a community ID. A command specifies its community; the server checks each referenced
resource belongs to it. Switching the display does not migrate assets.

## Communities and membership

- Limits: three owned and six active memberships, owned included. Pending invitations
  and dormant memberships do not consume a slot. Recommended scope: per character.
- Owner creation atomically creates community, owner membership, currency and policies.
  M3 adds the default categories when the inventory feature becomes available. Admission and ownership-transfer limits use database locking.
- Only the owner issues permanent, revocable nine-character invitations with unlimited uses. Joining requires explicit
  acceptance of community visibility/data rules; possession of a code is not membership.
- Intended behavior (A20): voluntary leave and owner removal deactivate membership
  without blocking invited rejoining. Blocking is separate and prevents rejoining
  until unblocked. Rejoin requires a valid invitation and available community slot;
  unblocking alone does not restore membership. M4 implements separate deletion
  and blocking. The deprecated M3 removal route retains remove-and-block semantics
  for old clients; the M4 interface uses the separate operations.
- Leave/removal cancels open trades, revokes effective grants, and makes inventory
  and community-history feeds inaccessible. Retained membership/asset rows do not
  authorize reads. Rejoin uses the same character/community identity and quota history.
  Owner must grant creation/currency/removal permissions again after rejoin.
- Default member roster shows active members only; owner may explicitly show
  dormant/blocked recovery rows. Retain assets and quota history internally.
- Dormant assets cannot be traded, consumed, or administratively adjusted; the owner
  can inspect dormant holding summaries for recovery, without reading letter bodies.
- Ownership transfer: current owner proposes an active member; recipient accepts;
  transaction checks limits and switches authority. Former owner remains an ordinary
  member. No owner can remove themselves or leave without transfer/deletion.
- Delete community: owner confirms its exact name; cancel trades and invitations; make
  it read-only/inaccessible to members. Purge only through the documented retention
  workflow. API retries cannot create a second deletion.
  Personal operation receipts and terminal trade outcomes remain available for
  reconciliation within retention; this does not restore inventory/community-history
  access. Operation metadata receipts are actor/participant-scoped and do not
  reveal later moderation reasons.

## Capabilities

| Action | Owner | Authorized member | Ordinary member |
| --- | --- | --- | --- |
| Membership, policies, ownership, categories, moderator grants | Yes | No | No |
| Community description / currency name | Yes | `community.moderate` | No |
| Grant Trading / Craft Potions | Yes | `members.gameplay.manage` | No |
| Create announcements | Yes | `announcements.manage` | No |
| Define/revise/retire potion recipes | Yes | `items.potion.define` | No |
| Issue/remove currency | Yes | `currency.manage` | No |
| Craft potions within own allowance | Yes | `items.potion.create` | No |
| Remove another active member's items | Yes | `inventory.remove` | No |
| Create letters within quota | Yes | Yes | Yes |
| Read owned letters, use potions, discard own holdings | Yes | Yes | Yes |
| Trade | Unless restricted | Unless restricted | Unless restricted |

Owner authority is derived from current ownership, not a role grant. Revocation is
effective on the next command; never trust long-lived token permission claims.
No capability holder may grant permissions or change community policy. Own-item
discard is separate from removing another member's holdings. Other inventory
inspection is limited to summaries required for authorized removal; letter text
is visible only to its current holder.

## Currency

One community-owned currency definition in 1.0: name, custom image icon, stable currency
ID. Owner may upload static PNG, JPEG/JPG or WebP, at most 128 pixels on either
axis; default coin icon is bundled. Changing/removing an icon preserves currency
identity and balances. M2's text symbol remains legacy compatibility data.
The server validates/normalizes images and enforces upload/community/owner limits;
see the [M3 feedback plan](../delivery/m2-feedback-m3-plan.md).
Balances and amounts are nonnegative signed-64-bit whole units internally, checked
for overflow. Wire amounts are decimal strings to preserve precision in future
clients. No float arithmetic. Negative adjustments require sufficient *available*
balance; reserved money cannot be removed. An administrative adjustment requires
a nonempty reason and records actor, recipient, signed delta, before/after balance.
Authorized minting is deliberately unrestricted by weekly points in the proposed
1.0; owner oversight and history manage abuse. Trade transfers conserve supply.

## Types, categories, and revisions

Types implement behavior. Categories label inventory and have allowed type IDs;
changing a category never changes item behavior or capability checks. Stable type
IDs are `potion` and `letter`. Owners cannot install types or executable scripts.
Seed Consumables (`potion`) and Books (`book`, `letter`, `note`). Retired
categories remain readable on old holdings, and cannot be selected for new content.

Owner or authorized recipe editor potion definition fields: name, category, description, plain-text use message,
positive integer creation cost, enabled state. Update creates an immutable revision.
Copies reference that revision. Same community/character/definition revision stacks;
different revisions remain distinguishable. Definition retirement blocks production,
but does not delete existing copies or prevent their consumption/trading.

Potion production submits definition ID, expected definition revision, and quantity.
Server verifies availability, grant, limits, budget, and current revision; cost is
`quantity * creationCost` with checked arithmetic. Debit usage, credit holdings, and
write operation/audit/idempotency records in a single transaction. Owner also uses
their own allowance. There is no bypass or arbitrary item-grant endpoint in 1.0.

## Weekly limits

- Recommended boundary: Monday 00:00 UTC through the following Monday, exclusive.
  Server time determines period; interface shows reset in local time.
- Owner sets a community-wide potion points limit and letter count limit. Each character
  has independent `(community, character, quotaKind, period)` usage.
- Community creation explicitly sets the first period's limits. The UI warns before
  creating a community with zero points because later changes start next period.
- Defaults: zero potion points until configured; five letters/week. Zero disables
  that creation path. Unused allowance does not roll over.
- Weekly policy edits apply next period; show current and scheduled limits. On
  first creation in a period, materialize its immutable policy snapshot.
- Potion cost edits affect future production immediately, without recalculating
  past spending. Stale preview requests fail with `definition_changed`.
- Creating one letter spends one count. Edits, trades, reads, or receiving letters
  do not spend it. No refunds for discard, use, administrative removal, or trading.
- Grant toggles, leave/rejoin, restarts, and device changes do not reset usage.
- No scheduled reset job is needed: derive the period and accumulate spending.

Defaults and hard ceilings are discoverable from the server and configurable as
documented in [architecture](../engineering/architecture.md). Weekly quotas limit
creation, not accumulated storage; inventory ceilings handle total growth.

## Letters

Each letter is a quantity-one instance with title, body, category, immutable author
identity, content revision, and first-trade timestamp. Recommended limits: title
80 characters, body 5,000; service ceiling may be raised through configuration.
No attachments, formatting commands, remote content, or cloned-letter endpoint.

Only the author while holding the letter may edit it, before first completed trade.
Offering it reserves it and blocks edits. Completed trade permanently locks contents;
returning it to the author does not unlock it. Readers see author, community, title,
body, and locked state. Drafts are local; creating the persistent item spends quota.

## Consumption and chat

There is no chat permission window, startup question or chat permission checkbox.
This supersedes the earlier startup-consent design at the maintainer's request
for plugin 0.0.6.1. Show the exact message and destination before Use; clicking
**Use one potion and post message** explicitly requests both consumption and
posting. Login, background refresh, reconnect and server events never post chat.

One explicit player Use action consumes exactly one available potion. Require
a valid chosen Emote/Say/Party/specific Linkshell destination before requesting
consumption. There is no additional permission step.
Count the final message as Unicode text elements,
maximum 50 by default; reject line breaks, control characters, command injection,
and unsupported game payloads. 1.0 has literal text, with no template substitution.
Channel prefix and game-supplied character prefix are outside the authored limit.
Test byte-length/game encoding separately; the character limit alone is insufficient.

Show exact message, channel, quantity cost, community, and character before the Use action.
The server commits consumption and returns operation ID plus immutable message.
Only that user action may attempt chat; server events never trigger chat.

Database commit and FFXIV delivery cannot be atomic. After an ambiguous request,
recover operation outcome before doing anything else. Replayed consumption responses
must not automatically send chat. If chat fails after commit, report the consumed
item and failed message; offer explicit manual resend with a warning about possible
prior delivery. Never auto-refund or auto-resend. Recheck channel after the response;
if unavailable, show failure without silently switching channel.

## Trading

Both parties must be active, unrestricted members of the same community. Initiation is
through the character context menu or community-member inventory flow; recipient accepts
before assets may be offered. No global lookup or plugin-user presence probing.
Recommended: one open trade/character/community and ten-minute expiry after initiation,
with the actual expiry shown. Offline recipient gets a pending invitation on next
sync if it has not expired. Assets are not offered until acceptance.

State machine: `invited -> open -> confirming -> completed`, with `cancelled` or
`expired` terminal alternatives. Offer-building edits increment a trade revision.
Once either player accepts, any edit request cancels the whole trade. Both first
approvals enter confirming; both final approvals are required to complete.
Server reserves offered quantities and currency; available = owned minus reserved.
Trade responses provide server-derived potion definition/message previews and
letter title/author summaries. They do not expose private letter bodies or the
sender's full inventory. Confirmation is based on those exact offered revisions.
Cannot consume, discard, remove, edit, or offer those reserved assets elsewhere.

Each acceptance and final confirmation names the reviewed trade revision. The
second final confirmation triggers
one transaction: lock participants/resources in deterministic order; recheck membership,
restriction, expiry, revision, holdings and capacity; exchange both sides; lock letters;
write ledger/history; release reservations; mark completed. A failed check rolls back
the entire exchange. Empty trades are rejected; one-sided gifts are allowed.

Cancellation/expiry releases reservations transactionally. Owner restriction/removal
must serialize with completion: either completion occurred first or cancellation
prevents it. Client disconnect alone is not cancellation; recover by GET trade.
Switching communities closes that client's unfinished trade via cancellation before the
UI switch; ambiguous cancellation must be reconciled. Completed trades cannot be
undone through cancel. Reservations and confirmations persist across server restart.

## M4 writing and trading implementation

Books is the seeded writing category. `book`, `letter` and `note` are explicit
plain-text subtypes, all spending the existing per-character `letters` allowance.
No subtype circumvents that weekly limit. New communities receive the Books default.
Existing category names, allowed types, IDs and versions are preserved during
upgrade, including legacy Correspondence rows; owners explicitly opt in to new
types or rename them through the owner category API. All written types have
immutable author identity, private held bodies and permanent editing lock after
first trade. Rich books/chapters remain future features.

Trading permits one active invited/open/confirming trade per character **per community**.
Both participants must be active and unrestricted for initiation, acceptance,
offer changes and confirmation. Offers reserve owned assets; both confirmations
refer to one saved revision. Edits are allowed before acceptance; any edit after
either first approval cancels the trade. Both first approvals enter final review;
both final approvals perform one atomic net exchange. One-sided gifts are allowed; an entirely empty trade is rejected.
Quantities and int64 balances are checked, and recipient capacity uses the net
result so full-inventory swaps remain possible. No creation allowance is refunded
or charged by transfer/cancellation.

Trade rows, participant slots, item previews and reservations live in PostgreSQL.
Removal, blocking, trade restriction or community deletion cancels affected trades
under the same database lock as completion. Expiry is fixed from invitation
creation (default ten minutes, discoverable policy) and never extended by edits.
Participant reads reconcile expiry; a bounded server sweep runs every 30 seconds.
Completed trades cannot be cancelled. Terminal participant recovery remains
available after removal without granting access to another inventory or text body.

## Retained resources and consent audit

Alongside the 3-owned/6-joined active slots, server 0.0.6.9 enforces an operator
retained-record budget of 30 communities per original creating character by default.
Deleted and transferred creations count until eligible physical purge. Transfer
never moves this resource accounting to its recipient. This prevents repeated
create/delete cycles retaining unlimited communities; separate characters remain
independent. The service returns a clear capacity error, not a data reset.

Each participant can record each trade approval stage once for a revision. A new
operation key cannot add the same consent again. Retrying the original key safely
recovers its receipt. Existing two-stage confirmation and cancellation rules apply.

## Member profiles

Each character has a separate optional description within each joined community.
Only that active character can edit it, including when another member is owner.
The description is plain text, permits ordinary line breaks, and is limited to
300 Unicode text elements; it can be cleared. Community members can read visible
member profiles. Leave/rejoin preserves the text under existing retention rules.
Members show name/description/trade; the owner sees individual permission controls
below. Additional authorized administration is collapsed. Main navigation keeps
vertical labels with Home, Inventory, Crafting, Members and Settings icons.

## Member currency and permission presentation

Member currency inputs have labels above them. Authorized viewers automatically
load a selected active character's balance, shown vertically as Total, Available
and Offered in trades, with Refresh beside the list. Reading another member's
balance still requires currency.manage or ownership; no privacy boundary changes.

Gameplay Permissions uses a positive Trading checkbox (enabled by default), Craft
Potions, Manage currency and item-removal authorizations. Trading needs no reason.
Manage membership contains a Moderator permissions section for Potion Creation
(define recipes), separate from the permission to craft copies. This authorizes
defining, revising and retiring recipes, not community policy/category management.
The two save buttons preserve the other section's saved permissions and do not
silently commit its unsaved checkbox draft. Owners retain automatic authority.

Membership action inputs have visible labels; confirmation checkboxes are removed.
Hold Shift to enable remove/block/unblock/ownership-proposal buttons. Hover text
explains this even while disabled. Required moderation reasons remain in place.

## Home, crafting guidance and announcements

General Moderation enables direct community-description and currency-name editing on
Home. Others see ordinary text. It does not grant any gameplay permissions.
Manage Gameplay Permissions is a separate owner-granted moderator action for
Trading and Craft Potions. Currency management, item removal, recipe definition,
General Moderation, announcement creation and gameplay-management delegation remain
owner-controlled moderation permissions in Members → Manage membership. No roles
are introduced. Owner authority stays automatic.

Crafting has Info, permitted Potions, authorized Potion recipes and Books tabs.
Info explains RP crafting, independent recipe/copy permissions, owner or authorized
moderator access, and weekly limits. Books/letters/notes remain available to every
active member under their allowance. Potions is hidden when Craft Potions is absent.

Home shows up to five clickable announcement titles, newest first; opening a title
shows the full plain-text message. Only owner or Create Announcements holders can
post. Older messages are deleted as new ones exceed five. Server-persisted unread
state is independent for each character/community. Opening a message marks only it
read, not the entire list. The Home icon and main launcher button show red pings
while unread retained messages remain across joined communities. The launcher focuses
an unread community's Home when switching is allowed; it does not open on arrival or
interrupt a trade. Community selection marks communities with unread announcements.

Owner Community Settings contains Currency (icon), Invitations and Delete Community tabs.
Home no longer has the combined names section or weekly allowance header. Weekly
Policy has Potions and Books subtabs, initially populated from active settings.
Each save schedules that feature for the next reset and preserves the other
feature's currently scheduled value. Unsaved inputs survive background refresh;
concurrent policy changes require reload instead of silent overwrite.

### Community profile overview and logo

The owner may rename the community and choose/replace/remove its logo by clicking the
profile image/placeholder. General Moderation continues to edit description and
currency name, not community name/logo. Ordinary members see read-only profile text.
Logo and currency icon are separate community assets. Publishing announcements remains
owner or Create Announcements authorization; inline Title/Message/Publish preserves
the five-message limit, full-message reader and per-character unread state.

### Current text limits

Community descriptions, character descriptions and potion descriptions are limited to
300 Unicode text elements each. Contents for all Books types (book, letter and
note) are limited to 500; titles retain their existing limit. Inputs stop accepting
excess text, including pasted text; server validation independently rejects excess
writes. Existing longer saved text is preserved for reading and must be shortened
when edited. Announcements retain their separate 120/5000 title/body limits.


## Groups within communities (testing)

Community owners may grant Create Groups (`groups.create`) and Manage Groups
(`groups.manage`) independently. The latter covers profiles and logos throughout
the community; neither grant permits changing moderator permissions. Any active
community member may read group profiles. Groups have independent names, descriptions
(up to 300 characters) and logos, with no fixed organisation type. They currently
share the community economy rather than receiving separate currency/inventories.
