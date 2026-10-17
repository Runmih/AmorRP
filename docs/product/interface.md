# Interface and player flows

## Shared display

The shared header contains the persistent **community dropdown**, **Join**,
**Create community** and **Leave** buttons. Join and Create open independent form windows. It has no
character line, currency summary, sync controls, timestamps or routine status rows.
Settings contains only the quick-launcher checkbox. Actual action results, pending
recovery and connection failures appear below the content only when needed; idle
feedback consumes no space. Use labels as well as color for errors, restrictions
and offline state. No network
or database work runs synchronously in the ImGui drawing callback.

```text
Amor RP
[Community v] [Join] [Create community] [Leave]
Home       | Inventory: [All] [Consumables] [Books]
Inventory  | [Search / type / category / sort]
Crafting   | [Stable item rows]  | [Selected details and permitted actions]
Members    |
Settings   |
```

Main navigation is **vertical**, with **horizontal subtabs**. Inventory is the
initial selection. Home is the community profile; owner profile/invitation/policy/icon
controls live with that profile. Crafting has Potions and Books; only owners see
recipe administration inside Potions. Members integrates applicable member actions.
There is no Owner main tab. Settings contains only General → Show quick launcher.
Unauthorized administration controls are hidden; server authority remains required.

## First use and authentication

1. The signed-out screen shows one **Log in with XIVAuth** button. It starts
   verification and opens the trusted XIVAuth page. After verification, show the
   pairing-code input and Finish login button; the user copies the browser-only
   code into the plugin they started. Show waiting/cancel during an active attempt and a short error
   when needed. Never ask for Square Enix credentials.
2. Use the release endpoint `https://amorrp-server.onrender.com/`. There is no
   address input, connection-check button, M1 tools or saved-character picker in
   the login screen. Existing saved login resumes automatically for the character.
3. After login offer Create community or Join with invitation. Display visibility
   information and remaining membership slots where those actions are performed.
4. There is no startup chat permission window or checkbox. The deliberate Use
   action requests posting the exact visible potion message to its chosen channel.

Local character switch immediately clears prior character views, stops event
subscriptions, and cancels pending UI actions. Authenticate the new character;
never display the former character's private letter contents under the new identity.

## Communities

The dropdown stays outside the tab contents and is available from every tab.
Selector lists only current character's active memberships and highlights owner
status. Display pending trade before switching; reconcile cancellation first.
On community removal or deletion show an explanation, clear sensitive caches and
select another available community. Empty membership list has actionable Create/Join.

Create: community name, description, currency name, default coin icon, weekly potion
points and weekly letter count. Default points zero is explained: "Set an allowance before creating
potions." Join: invitation code, community summary, membership disclosure, confirmation.
Invitation codes are copyable only when created, not shown in logs. After a
confirmed join, clear the entered code and reset visibility acceptance. After
confirmed creation, reset its draft to server defaults and select the new community;
keep creation and owner-edit drafts separate. Retain inputs on failed/unknown outcomes.
Owner currency settings upload/preview PNG, JPEG/JPG or static WebP up to 128x128,
with a bundled fallback icon and a clear oversize/invalid-file message.

## Compact consumables

Small movable window with community, currency, and locally pinned potions. Rows show
name, available/owned quantity, details tooltip, and Use. Pinned item revisions are
identified consistently; obsolete pins can be removed. Empty state links to extended
inventory. Reserved count is visible. Offline state disables Use.

Use dialog shows community, potion revision, exact message, selected destination,
"Consumes 1", and **Use one potion and post message**. There is no separate chat
permission window, startup question or permission checkbox.
Destination picker includes specific Linkshell slots/names available to the current
character; remember selection per character/community. No automatic fallback to current
chat channel. Show explicit failed/unknown outcome rather than optimistic depletion.

## Extended inventory

Search, category and type filters, stable sorting, pagination, and selected details
pane. Columns: item name, type/category, available quantity, reserved quantity.
Details: description, revision, author for letters, potion use message, applicable
actions. Display letter body only after authorized detail request. Default item
icons are bundled. Custom currency icons load only from the configured backend
under current community authorization; no user-supplied remote URLs. Actions include
pin/unpin, read, use, discard, eligible edit, and trade. Discard confirms quantity and irreversibility.

## Creation

- **Potion:** select enabled definition; view current revision, cost/copy, quantity,
  total cost, remaining weekly points, reset time. Disable production if budget is
  insufficient. A stale-definition conflict refreshes preview and requires review.
- **Books:** choose Book, Letter or Note, then title, plain text, category, shared writing allowance,
  and reset time. Save draft locally without quota cost; Create clearly explains
  this produces a persistent inventory item. Local drafts are character/community scoped.
- Capability revoked while dialog is open: show rejection and refresh grants.

## Trade

Context menu entry "Amor RP: Trade" is limited to player characters where the
context supplies a reliably resolved home world. Resolve target within own community's
consented member roster. If several joined communities overlap, default to active community
and allow an explicit community choice before initiation. Missing context identity is
an explanatory disabled entry; do not guess the currently visited world.

Recipient sees sender, community, expiry, Accept/Cancel in a separate trade window.
The left column lists the player inventory; the right stacks their own offer above
the other player’s offer. Right arrows add a chosen quantity; left arrows remove
it. Currency saves on focus loss and strips leading zeros. Offers save automatically;
there are no Publish/Reload buttons. Show quantities and acceptance status. Offered potions show their original recipe revision, description and
use message; letters show title and author, with contents sealed until received. Books/notes use the same sealed writing behavior.
These previews come from the trade response, without exposing another inventory.
Before acceptance, peer edits synchronize automatically without clearing local
drafts. Accept offer queues any pending local save first. Both players accepting
opens a final popup showing exactly what each sends and receives. Both must approve
that same revision there. Editing after either acceptance, cancelling or closing
the trade window ends the whole trade. A transport disconnect retains the trade
until its displayed expiry; cancellation takes effect when committed by the server.
Actual in-flight writes and unresolved outcomes still use protected recovery. Cancellation,
restriction, expiry, completion and reconnect all have distinct messages. Unknown
completion shows "Checking trade result" until authoritative reconciliation.

## Owner and delegated tools

- Members: character/home world, individual action checkboxes, trading restriction,
  Remove, Block and Unblock as separate actions. Invitations are in Home. Default to active members, including for owners.
  Owner-only Show inactive members exposes dormant/blocked recovery rows.
  Member removal has its own mandatory reason and confirmation, with rejection
  visible until acknowledged or superseded by another action. Do not show
  online/offline plugin usage.
- Potions: definition list, revision editor, enable/retire, current creation cost,
  warning that edits affect new copies. Community owner alone defines recipes.
- Policies: current weekly limits and next-week scheduled values with effective date.
- Categories: Consumables and Books are seeded. Arbitrary category creation is not part of the player workflow; existing custom category data and compatibility APIs remain.
- Currency: authorized recipient, signed adjustment, mandatory reason, preview balance.
- Removal: authorized holding summaries, quantity, mandatory reason; no letter-body
  read privilege. Reserved assets show why removal is unavailable.
- History: owner administrative events; members see personal operations and their
  own completed trades. Letter bodies and authentication secrets never appear.
- Ownership/deletion: clear consequences, recipient consent, typed community-name confirmation.

## Settings and failure handling

Settings shows only the quick-launcher visibility checkbox. Chat destination and
pins live with consumable actions; support is the project’s GitHub Issues page.
The release server address is managed in
one source location and has no player-facing editor. Existing credentials, caches
and pending commands stay bound to their original HTTPS origin; changing a release
endpoint never relabels or forwards another origin's credentials.

| State | Display/behavior |
| --- | --- |
| Loading/empty | Skeleton/status then useful empty-state action; not a blank window. |
| Offline | Last sync time; cached own summaries read-only; no queued mutations. |
| Expired login | Sign-in required; preserve nonsecret drafts; stop privileged actions. |
| Quota exhausted | Remaining amount, cost, exact local reset date/time. |
| Restricted trading | Explicit restriction; other allowed inventory features remain. |
| Request timeout | Keep operation key; query outcome; no new-key retry or assumed success. |
| Stale revision | Refresh, explain change, require renewed review. |
| Maintenance/update required | Server message and compatible-version instructions. |
| Server error | Safe message and request ID for support; no raw stack traces. |

Before 1.0, validate keyboard navigation, UI scaling, long names/letters, percent
signs, Unicode, two potion revisions with identical names, and unfamiliar-user flows
with testers. Product text must explain actions, not internal API mechanisms.

## M2 test-build navigation

`/amorrp` opens Character login plus four tabs: Communities, Members & currency,
Owner settings, History & devices. Communities shows the community selector, your currency,
join visibility acceptance and creation with first-week limits. Members selects
stable roster IDs for direct grants, restrictions, removal, coin changes and
ownership proposals. Owner settings exposes current/scheduled policies, renaming,
invitations, ownership cancellation and exact-name deletion. History/devices shows
personal or owner history and session revocation. Save/renewal work is asynchronous
and disables changes while pending. Unresolved commands retain their retry key and
require retry/receipt reconciliation before another mutation.

The selected community is saved per backend/verified character. Inventory, compact
consumables and trading tabs arrive at their milestones. Diagnostics stays under
an explicit M1 section; its chat test is not a currency/item use action.

## Refresh and action feedback (M3)

Keep manual refresh, last successful update and synchronization details in
Settings → Connection. The shared header contains community selection and Join/Create community entry buttons. Connection
failures remain visible below the content rather than being hidden with diagnostics.
Refresh after committed
mutations and every 60 seconds while an authenticated product window is visible;
skip overlapping/in-flight actions and back off on connection failures.
Refresh read models without replacing local drafts, clearing selection unnecessarily
or clearing retained action feedback. Community/character switching clears private views
and cannot redirect a pending command into the newly selected context.

Separate synchronization status from action results. Loading/refreshed messages
must never overwrite a removal or other command error. Explain required reasons,
authority denials and stale versions; expandable troubleshooting shows sanitized
HTTP status, error code, request ID and operation key without credentials/content.
Only a committed removal appears as a successful history event.
See the [M2 feedback plan](../delivery/m2-feedback-m3-plan.md) for implementation order.


## M3 test-build navigation

Main window `/amorrp` uses the shared dropdown above Communities, Members & currency,
Consumables, Inventory, Create items, Owner settings and History & devices.
`/amorrp compact` or Open compact consumables toggles a separate small window with
the same character/community selector, pinning, exact use preview and chosen channel.
Both visible windows share one async request coordinator and refresh clock.
M3 includes the functional flows above; final styling remains M5. Inventory/use
and private-letter acceptance are in the [M3 guide](../delivery/m3-install-test.md).

## M4 implemented behavior (supersedes M2/M3 navigation above)

Vertical Home / Inventory / Crafting / Members / Settings; Inventory
subtabs All / Consumables / Books; Crafting Potions / Books. All and Books use
selectable item rows and a details pane. Members uses an explicit selected holding
and one removal action with quantity/reason/confirmation. Remove permits rejoining;
Block prevents it; Unblock leaves membership inactive. Owner authority is implicit.

Background reads do not disable the window, switch tabs or reset local forms.
Pending mutations temporarily disable write actions; reads, navigation, selection
and drafts remain usable. Writes are serialized because only one unresolved
mutation may exist per active character/backend. Confirmed item responses update
quantities immediately; affected data refreshes quietly. General state refreshes
once per visible minute; trade data polls independently, including invitations
when the main window is closed. A newly received invitation marks the launcher
trade icon; the player opens the trade window when ready.
Saved login is preserved. Plugin 0.0.6.1 removes the separate chat permission flow.

A selected game-context character resolves against the complete current community's
verified roster. The user reviews the target, then invites. In the separate trade
window inventory is left, own offer top-right and peer offer below. Arrows and
currency focus loss save automatically, preserving drafts through peer updates.
Both accept, then both approve the exact final popup; an edit after acceptance
starts cancels the deal. Original potion messages/revisions and sealed writing
attribution remain visible. A community switch requests cancellation before
changing private context. Logout/disconnect leaves the server trade recoverable;
expiry releases reservations. This is a beta interaction implementation; M5 still
owns final style, scaling/accessibility review and deployment/support operations.

Plain-text books have one body with the same current title/body limits as letters;
chapters, pagination and attachments are future work. Existing API/DTO names such
as `Letter` remain compatibility names for written instances; typeId controls the
book/letter/note behavior. See [M4 tests](../delivery/m4-install-test.md).

## M5 0.0.6.0 usability checkpoint

The main and trading navigation above remains. Inventory now uses resizable
list/details columns with separate available/reserved counts and automatic filters;
search commits on Enter/focus loss without background reads applying unfinished
text. Discard is tucked into an explicit selected-item action. Crafting exposes
Potions, owner-only Potion recipes and Books subtabs; lists and selected editors
replace stacked production/recipe forms. Validation notes reserve their height,
and exact recipe messages/allowances/costs remain visible. Books have a scrollable
read-only text view and validated editing.

Members has a roster and selected-member panel with contextual Overview, Items,
Permissions and Membership subtabs. Unauthorized tabs/actions are hidden. Items
loads summaries and keeps the selected removal name/quantity/reason/confirmation
together. Settings provides Support & test build with the agreed GitHub Issues
link. No external report is sent automatically. Live scaling/rendering acceptance
remains part of the [M5 test](../delivery/m5-install-test.md).

## Community entry windows — plugin 0.0.6.3

Join and Create community sit beside the shared dropdown, including in the compact
window. Each opens a separate movable/resizable window with its respective form.
Settings General retains current membership controls; it no longer duplicates
these forms. Empty views direct users to the header buttons.

Form drafts survive closing/reopening and ordinary background updates. A committed
join/create (including receipt recovery) resets and closes only its matching form
and selects the resulting community. Rejections leave the form open with feedback;
unknown outcomes keep original-key retry/receipt controls. Server permissions and
community limits remain authoritative. Changing character/signing out closes the community
forms and clears their drafts so an invitation is not submitted as another character.

The main navigation has no Trades tab. Trading remains in its dedicated window,
opened through the character context menu, Members → Trade with this member, or
the launcher (which marks incoming invitations). Removing the navigation entry does not change trade
polling, reservation/recovery or the two-stage confirmation flow.

## Quick launcher — plugin 0.0.6.4

A separate titleless, movable launcher has exactly two icon buttons: book for the
main window and exchange arrows for trading. It has no visible text labels or
additional controls. There are no hover boxes. A red badge is drawn on the
trade icon while incoming requests await a response; no image asset or audio is
needed. This is a separate window from compact consumables.

The maintainer chose **badge only**: receiving an invitation never opens or focuses
the trade window automatically. Clicking the trade icon opens the current community's
request first, otherwise the pending request expiring soonest in another joined
community. If that would interrupt a current trade or unresolved write, the current
window explains what must finish first. Opening never accepts or confirms a trade.

Badges cover all known joined communities. The selected community keeps its existing trade
poll, while other joined communities are checked about every 15 seconds. Polling runs
while the main window is closed. These are authenticated existing read endpoints,
not a new server notification service. Requests disappear from the badge after
acceptance, cancellation, expiry or lost membership. Changing character/signing
out clears notification data; stale replies do not revive a resolved invitation.
Network failures back off, retaining last known requests until expiry and exposing
refresh trouble inline in the trade window.

The launcher appears by default while an FFXIV character is loaded. Its position
is retained by Dalamud. Drag its empty padding to move it. Settings → General →
Show quick launcher controls visibility; `/amorrp launcher` toggles it and
`/amorrp trade` opens trading directly. Visibility is an additive saved setting,
with no change to protected login or pending-operation data.

## Interface cleanup — plugin 0.0.6.5

This supersedes the historical Settings and feedback layouts above. General has
only Show quick launcher. Connection, Login & devices, History and Support & test
build tabs and the main navigation’s Compact view button are removed. Their
background history/device reads are removed as well; server audit/session APIs
are unchanged. `/amorrp compact` still opens the existing consumables view.

The shared header is dropdown / Join / Create community / Leave. Leave uses the same
membership DELETE as before, with a fresh membership ETag when needed. It is
disabled for owners (transfer ownership first), unresolved writes and active
trades. Receiving an ownership offer and accept/decline actions live on Home.

There are no hover tooltip boxes. Validation stays inline; explicit Action details
still shows diagnostic text. Normal action/trade feedback appears in a bottom footer, with at most three recent results. Each lasts 20 seconds or
until main/trade window reopening. Hovering or clicking Action details, or keeping its popup open, pauses that result’s countdown. Uncertain operations keep their retry key,
receipt controls and diagnosis until resolved. Active connection errors and trade
validation remain visible; periodic polling cannot restore old terminal outcomes.

Character context-menu Trade opens trading and automatically searches all pages
of the selected community’s verified active roster, matching the game character name
and home world only to resolve the stable character ID. No Find in shared community
button or automatic invitation is used. If unmatched, explain the shared-community
requirement; switching communities retries. Late results are bound to the originating
community/character view and cannot override a manual dropdown choice.

## New-login pairing — server 0.0.6.2 / plugin 0.0.6.6

The initial signed-out screen is still one login button. XIVAuth approval now
leads to an explicit browser-to-plugin pairing step before any session is issued.
The browser presents a one-time code, Copy pairing code, a warning against shared
links/code sharing and a manual Close tab. It stays open rather than using the
historical eight-second timer. In Amor RP, paste the code and press Finish login;
Cancel abandons the local attempt, which expires on the server. Pairing codes are
never saved in plugin configuration. Retries retain the same in-memory operation
key; a fresh login is needed after plugin restart, expiry or lockout. Saved login
and automatic renewal do not repeat pairing. A server without pairing support is
rejected for new logins rather than silently using the insecure older handoff.


## 0.0.6.16 usability update

Action feedback names the completed action and explains failures in plain language.
HTTP status, server code, request ID and action ID remain available through Details;
connection and login failures also have a Details button. Quiet refreshes retry after
5, 10, 20, 40 and then 60 seconds while preserving last-confirmed data and drafts.
Unknown write outcomes still require the saved action's retry/result check.

Inventory, member profiles and crafting forms stack their columns when space is
limited; navigation and breakpoints follow the current font size. Inventory type
icons keep their text labels. Trades show the current character's total currency,
amount available for this offer (including currency already reserved by this trade),
and draft offer amount. The peer's private balance is never requested.

Member search matches names and home worlds across the full community on the server,
before pagination. Submit with Enter or leave the field; further pages retain the
same search. Search cursors cannot be reused for a different search or community.

Member profiles include Your private notes, separate from the shared character
description. Notes allow 300 characters, are encrypted for the current Windows user,
and stay on this computer. They are keyed by server, signed-in character, community and
viewed character; they are never uploaded or visible to community owners. Save private
note persists changes; saving an empty note deletes it.

Community invitations contain nine unambiguous uppercase letters/digits, accept lowercase
entry, and never expire or run out of uses. Owners can copy a newly created code
and delete an invitation to invalidate it. Codes remain hashed and are returned only
at creation. Joining requires a verified login and has a separate per-network request
budget. Existing usable invitations become permanent on upgrade; expired, exhausted
or revoked invitations remain unavailable. Existing long codes remain accepted.
