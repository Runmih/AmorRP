# API contract and endpoint catalog

The current implementation has one model: **Seasons**, with RP groups inside each
season. The wire-support floors are client 0.4.0.3 and server 0.8.0.0; package versions are independent. The application's API is
v2; XIVAuth separately uses its own API v1. No old application routes or community
mode are exposed. [OpenAPI](openapi.yaml) records implemented and planned operations.

## Conventions

- A bearer session proves the provider-verified character identity. Client hints
  and public Lodestone IDs never grant authority.
- POST /api/v2/seasons requires the global seasons.create permission. Configured
  administrators receive it automatically and may grant/revoke it with a reason.
  Individual season/group roles do not grant global creation authority.
- Creation returns a draft. Activation and archival are administrator-only,
  versioned, idempotent transitions. One season is active at a time.
- Every verified character belongs to every non-deleted season automatically,
  including drafts and archives. Season membership has no join, leave, removal,
  block or restore commands. User bans belong to separate account moderation.
  New seasons do not inherit assets or scoped grants; archives stay read-only.
  Shared season access does not grant another player's private records or items.
- IDs are UUIDs, monetary amounts are bounded whole-number decimal strings, and
  all scoped resources use seasonId. Internal historical database names remain
  mapped to the same data; they do not expose another product model.
- Mutations use UUIDv7 Idempotency-Key and documented If-Match preconditions.
  Retrying a completed key recovers its original receipt. A changed request cannot
  reuse the key. Preserve unresolved operation keys and read their receipts.
- Error bodies include a user-facing title, stable code and requestId. Session
  credentials stay out of URLs, clipboard diagnostics and ordinary logs.

Group-scoped recipes require active group membership and the matching discipline
permission. Authors can read recipes they can edit. Crafting still requires the
crafting grant. The season owner can set a per-group cap, and retirement frees a
slot. Group recipes stay out of the season shop. Finished items can still trade
and carry their original recipe revision and optional consumable effect.


## Endpoint catalog

| Method | Path | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v2/seasons/context` | Active season and authenticated administrator capabilities | Verified character session; lifecycle summaries are administrator-only |
| POST | `/api/v2/seasons/{seasonId}/activation` | Activate a draft season and archive the current season | Server-configured provider-verified Lodestone administrator allowlist; exact name and current ETag required |
| POST | `/api/v2/seasons/{seasonId}/archive` | Freeze the active season without deleting its assets or records | Server-configured provider-verified Lodestone administrator allowlist; exact name and current ETag required |
| GET | `/health/live` | Process liveness | Public |
| GET | `/health/ready` | Cached database and startup readiness; failure uses 503 | Public |
| GET | `/api/v2/capabilities` | Versions, maintenance, types, permissions and public limits | Public |
| POST | `/api/v2/auth/login-attempts` | Start browser character login | Public bootstrap; secret retry credential; rate limited |
| GET | `/api/v2/auth/login-attempts/{attemptId}` | Poll login status without session secrets | LoginAttempt |
| DELETE | `/api/v2/auth/login-attempts/{attemptId}` | Cancel own pending login attempt | LoginAttempt |
| POST | `/api/v2/auth/login-attempts/{attemptId}/exchange` | Pair verified browser approval and redeem once for session | LoginAttempt |
| GET | `/auth/xivauth/callback` | Registered browser OAuth callback | Exact callback; one-use state; provider exchange |
| GET | `/auth/assets/login-background-{contentHash}.jpg` | Bundled public login wallpaper | Public; exact current image hash only |
| POST | `/api/v2/auth/sessions/refresh` | Rotate refresh credentials and revalidate identity | Valid refresh credential; replay key protected |
| DELETE | `/api/v2/auth/sessions/current` | Revoke current session and close its event connection | Session |
| GET | `/api/v2/me` | Current verified character | Session |
| GET | `/api/v2/me/sessions` | Own character sessions | Session |
| DELETE | `/api/v2/me/sessions/{sessionId}` | Revoke own character session | Session owner |
| GET | `/api/v2/seasons` | Current character active seasons | Session |
| POST | `/api/v2/seasons` | Create a season draft | Authenticated seasons.create permission; configured Lodestone administrators receive it automatically |
| GET | `/api/v2/seasons/{seasonId}` | Season and own permissions/balance | Active member |
| PATCH | `/api/v2/seasons/{seasonId}` | Edit season name/description | Owner; season.moderate can change description only |
| GET | `/api/v2/seasons/{seasonId}/policies` | Current and next weekly policies | Active member |
| PUT | `/api/v2/seasons/{seasonId}/policies` | Schedule next-week limits and optionally update Gathering settings in one save | Owner |
| GET | `/api/v2/seasons/{seasonId}/members` | All verified players in the season | Active member |
| PUT | `/api/v2/seasons/{seasonId}/members/{characterId}/capabilities` | Replace individually granted actions | Owner; members.gameplay.manage can change Trading and items.potion.create only |
| PUT | `/api/v2/seasons/{seasonId}/members/{characterId}/trade-restriction` | Restrict/restore both trade directions and cancel affected trades | Owner; active nonowner |
| GET | `/api/v2/seasons/{seasonId}/members/{characterId}/holdings` | Minimal holding summaries for authorized removal; no letter bodies | Self read, or inventory.remove for summaries; no private bodies |
| GET | `/api/v2/seasons/{seasonId}/members/{characterId}/balances` | Balances for authorized currency adjustment | Self read, or currency.manage for another active member |
| POST | `/api/v2/seasons/{seasonId}/ownership-transfers` | Propose transfer to active member | Owner |
| GET | `/api/v2/seasons/{seasonId}/ownership-transfers/{transferId}` | Read transfer status | Current owner or proposed recipient |
| POST | `/api/v2/seasons/{seasonId}/ownership-transfers/{transferId}/accept` | Accept pending transfer within owner caps | Proposed recipient; active member |
| POST | `/api/v2/seasons/{seasonId}/ownership-transfers/{transferId}/cancel` | Cancel pending transfer | Owner or proposed recipient |
| GET | `/api/v2/seasons/{seasonId}/currency` | Season currency definition | Active member |
| PATCH | `/api/v2/seasons/{seasonId}/currency` | Rename currency without replacing identity | Owner; season.moderate can change name only |
| GET | `/api/v2/seasons/{seasonId}/currency/icon` | Read the season currency icon | Active member |
| PUT | `/api/v2/seasons/{seasonId}/currency/icon` | Upload or replace the season currency icon | Owner |
| DELETE | `/api/v2/seasons/{seasonId}/currency/icon` | Remove the custom currency icon | Owner |
| GET | `/api/v2/seasons/{seasonId}/logo` | Read the private season logo | Active member |
| PUT | `/api/v2/seasons/{seasonId}/logo` | Upload or replace the season logo | Owner |
| DELETE | `/api/v2/seasons/{seasonId}/logo` | Remove the season logo | Owner |
| GET | `/api/v2/seasons/{seasonId}/balances` | Own available/reserved currency | Active member |
| POST | `/api/v2/seasons/{seasonId}/currency/adjustments` | Issue/remove whole-unit currency with reason | Owner or currency.manage; target active |
| GET | `/api/v2/seasons/{seasonId}/categories` | Season categories including retired labels | Active member |
| POST | `/api/v2/seasons/{seasonId}/categories` | Create bounded organizational category | Owner |
| GET | `/api/v2/seasons/{seasonId}/categories/{categoryId}` | Category details and concurrency token | Active member |
| PATCH | `/api/v2/seasons/{seasonId}/categories/{categoryId}` | Edit category without changing item behavior | Owner |
| POST | `/api/v2/seasons/{seasonId}/categories/{categoryId}/retirement` | Retire category for new content; preserve old holdings | Owner |
| GET | `/api/v2/seasons/{seasonId}/item-definitions` | Potion definitions with latest revisions | Active member |
| POST | `/api/v2/seasons/{seasonId}/item-definitions` | Define potion without issuing copies | Owner or items.potion.define |
| GET | `/api/v2/seasons/{seasonId}/item-definitions/{definitionId}` | Definition revision; latest by default | Active member |
| PATCH | `/api/v2/seasons/{seasonId}/item-definitions/{definitionId}` | Create immutable definition revision | Owner or items.potion.define |
| POST | `/api/v2/seasons/{seasonId}/item-definitions/{definitionId}/retirement` | Retire production without deleting existing copies | Owner or items.potion.define |
| GET | `/api/v2/seasons/{seasonId}/quotas` | Own current usage, limits, reset time | Active member |
| GET | `/api/v2/seasons/{seasonId}/inventory` | Own holding summaries with filters | Active member |
| GET | `/api/v2/seasons/{seasonId}/inventory/{holdingId}` | Own holding and type-specific details | Current holder; active member |
| POST | `/api/v2/seasons/{seasonId}/inventory/potions` | Spend own potion points to produce copies | Owner or items.potion.create |
| POST | `/api/v2/seasons/{seasonId}/inventory/letters` | Spend own letter allowance to create individual letter | Active member |
| GET | `/api/v2/seasons/{seasonId}/letters/{letterId}` | Read currently owned letter and edit concurrency token | Current holder; active member |
| PATCH | `/api/v2/seasons/{seasonId}/letters/{letterId}` | Edit untraded, unreserved letter held by its author | Author and current holder |
| POST | `/api/v2/seasons/{seasonId}/inventory/{holdingId}/use` | Consume one available potion and return literal chat text | Current holder; active member; potion type |
| POST | `/api/v2/seasons/{seasonId}/inventory/{holdingId}/discard` | Discard own available quantity; no quota refund | Current holder; active member |
| POST | `/api/v2/seasons/{seasonId}/inventory/removals` | Remove another active member holding with reason | Owner or inventory.remove; target active |
| GET | `/api/v2/seasons/{seasonId}/trades` | Own trades, pending invitations and recent outcomes | Verified player; only own participant trades, including terminal recovery |
| POST | `/api/v2/seasons/{seasonId}/trades` | Invite same-season unrestricted character to trade | Active unrestricted member; same for target |
| GET | `/api/v2/seasons/{seasonId}/trades/{tradeId}` | Read own trade and recover outcome | Participant; terminal status remains available after removal |
| POST | `/api/v2/seasons/{seasonId}/trades/{tradeId}/accept` | Recipient accepts unexpired invitation | Recipient; both active/unrestricted |
| PUT | `/api/v2/seasons/{seasonId}/trades/{tradeId}/offers/mine` | Replace own offer; reserve resources; cancel if acceptance started | Participant; both active/unrestricted |
| POST | `/api/v2/seasons/{seasonId}/trades/{tradeId}/confirm` | Accept exact revision; both approvals open final review | Participant; both active/unrestricted |
| POST | `/api/v2/seasons/{seasonId}/trades/{tradeId}/final-confirm` | Approve final review; both final approvals atomically complete | Participant; both active/unrestricted |
| POST | `/api/v2/seasons/{seasonId}/trades/{tradeId}/cancel` | Cancel/decline own trade and release reservations | Participant; never undo completed trade |
| GET | `/api/v2/seasons/{seasonId}/history` | Personal season operation history | Active member of a non-deleted season; own actor/subject entries |
| GET | `/api/v2/seasons/{seasonId}/audit` | Administrative season history without private content | Owner |
| GET | `/api/v2/operations/{operationId}` | Own operation receipt after ambiguous request | Actor or involved trade participant |
| GET | `/api/v2/operation-keys/{idempotencyKey}` | Own key status; unknown key returns 404 | Authenticated actor only; excludes login/refresh keys |
| POST | `/api/v2/me/export-requests` | Planned: Request own-character export for operator workflow | Character; rate limited |
| POST | `/api/v2/me/deletion-requests` | Planned: Request deletion; resolve season ownership first | Character; recent reauthentication |
| GET | `/api/v2/me/data-requests/{requestId}` | Planned: Read own export/deletion request status | Request owner |
| GET | `/api/v2/me/data-requests/{requestId}/download` | Planned: Stream approved unexpired own-character JSON export | Request owner; ready export only |
| GET | `/api/v2/events` | Planned: Upgrade authenticated connection to recipient-filtered WSS events | Session; active season member |
| POST | `/api/v2/feasibility/login-attempts` | Start temporary XIVAuth feasibility login | LoginBootstrap |
| GET | `/api/v2/feasibility/login-attempts/{attemptId}` | Poll temporary login without session secrets | LoginAttempt |
| GET | `/auth/xivauth/feasibility-callback` | One-use PKCE feasibility callback | Exact HTTPS callback, one-use state and S256 PKCE |
| POST | `/api/v2/feasibility/login-attempts/{attemptId}/exchange` | Exchange temporary attempt for five-minute probe session | LoginAttempt |
| GET | `/api/v2/feasibility/identity` | Check temporary authenticated reconnect | Session |
| DELETE | `/api/v2/feasibility/session` | Revoke temporary probe session | Session |
| GET | `/api/v2/seasons/{seasonId}/members/{characterId}` | Read current member and ETag | Verified player |
| PATCH | `/api/v2/seasons/{seasonId}/members/{characterId}/holdings/{holdingId}` | Reduce owned quantity without allowance refund | Self or inventory.remove; mandatory reason and active target for administration |
| DELETE | `/api/v2/seasons/{seasonId}/members/{characterId}/holdings/{holdingId}` | Discard entire unreserved holding | Self or inventory.remove; mandatory reason and active target for administration |
| PATCH | `/api/v2/seasons/{seasonId}/members/{characterId}/description` | Edit own season character description | Active member, self only |
| GET | `/api/v2/announcements/unread` | Unread announcement counts for active joined seasons | Session; active memberships in nondeleted seasons |
| GET | `/api/v2/seasons/{seasonId}/announcements` | Newest five announcement titles, with own unread state | Active member |
| POST | `/api/v2/seasons/{seasonId}/announcements` | Post announcement and delete all but newest five messages | Owner or announcements.manage |
| GET | `/api/v2/seasons/{seasonId}/announcements/{announcementId}` | Read full retained message without marking it read | Active member |
| PUT | `/api/v2/seasons/{seasonId}/announcements/{announcementId}/read` | Mark this message read for the current character | Active member; own read state |
| GET | `/api/v2/seasons/{seasonId}/groups` | listCommunityGroups | Active season member |
| POST | `/api/v2/seasons/{seasonId}/groups` | createGroup | Season owner or groups.create |
| GET | `/api/v2/seasons/{seasonId}/groups/{groupId}` | readGroup | Active season member |
| PATCH | `/api/v2/seasons/{seasonId}/groups/{groupId}` | editGroup | Group owner only |
| DELETE | `/api/v2/seasons/{seasonId}/groups/{groupId}` | deleteGroup | Group owner or season Manage Groups (deletion only) |
| GET | `/api/v2/seasons/{seasonId}/groups/{groupId}/logo` | readGroupLogo | Active season member |
| PUT | `/api/v2/seasons/{seasonId}/groups/{groupId}/logo` | replaceGroupLogo | Group owner only |
| DELETE | `/api/v2/seasons/{seasonId}/groups/{groupId}/logo` | removeGroupLogo | Group owner only |
| GET | `/api/v2/seasons/{seasonId}/group-hub` | readGroupHub | Active season member |
| GET | `/api/v2/me/favorite-group-events` | favoriteGroupEvents | Current verified session; active season memberships |
| GET | `/api/v2/seasons/{seasonId}/groups/{groupId}/private` | readPrivateGroup | Active season and group member; no moderator bypass |
| POST | `/api/v2/seasons/{seasonId}/groups/{groupId}/join` | joinGroup | Active season member; open group or correct password |
| DELETE | `/api/v2/seasons/{seasonId}/groups/{groupId}/membership` | leaveGroup | Current group member; group owner cannot leave |
| PUT | `/api/v2/seasons/{seasonId}/groups/{groupId}/favorite` | favoriteGroup | Active season member |
| DELETE | `/api/v2/seasons/{seasonId}/groups/{groupId}/favorite` | unfavoriteGroup | Active season member |
| PUT | `/api/v2/seasons/{seasonId}/groups/{groupId}/notice` | saveGroupNotice | Active group member with enabled feature and its owner or staff grant |
| DELETE | `/api/v2/seasons/{seasonId}/groups/{groupId}/notice` | removeGroupNotice | Active group member with enabled feature and its owner or staff grant |
| POST | `/api/v2/seasons/{seasonId}/groups/{groupId}/events` | createGroupEvent | Active group member with enabled feature and its owner or staff grant |
| PUT | `/api/v2/seasons/{seasonId}/groups/{groupId}/events/{eventId}` | editGroupEvent | Active group member with enabled feature and its owner or staff grant |
| DELETE | `/api/v2/seasons/{seasonId}/groups/{groupId}/events/{eventId}` | cancelGroupEvent | Active group member with enabled feature and its owner or staff grant |
| POST | `/api/v2/seasons/{seasonId}/groups/{groupId}/announcements` | postGroupAnnouncement | Active group member with enabled feature and its owner or staff grant |
| PUT | `/api/v2/seasons/{seasonId}/groups/{groupId}/features` | Save group features | Active season owner only; group membership not required; no private content access |
| PUT | `/api/v2/seasons/{seasonId}/groups/{groupId}/members/{characterId}/capabilities` | Save group staff permissions | Active group owner only; active group/season target member; enabled features only |
| GET | `/api/v2/seasons/{seasonId}/groups/{groupId}/treasury` | Read group treasury balance | Active season and group member; treasury enabled |
| GET | `/api/v2/seasons/{seasonId}/groups/{groupId}/treasury/records` | List group treasury receipts | Active group member with treasury.records and enabled treasury; or former owner of deleted group |
| POST | `/api/v2/seasons/{seasonId}/groups/{groupId}/treasury/deposits` | Deposit own coins into group treasury | Active season and group member; treasury enabled; own available coins only |
| POST | `/api/v2/seasons/{seasonId}/groups/{groupId}/treasury/withdrawals` | Withdraw treasury coins to own character | Active group owner or treasury.withdraw staff; treasury enabled; pay caller only |
| GET | `/api/v2/me/treasury-returns` | List own pending disband returns | Current verified character; own pending disband returns only |
| POST | `/api/v2/seasons/{seasonId}/groups/{groupId}/treasury/return` | Collect disbanded group coins into original account | Current verified character who owns the deleted group; original retained account |
| GET | `/api/v2/seasons/{seasonId}/billing-policy` | Read billing settings | Active season member |
| PUT | `/api/v2/seasons/{seasonId}/billing-policy` | Save season debt and tax policy | Active season owner only |
| GET | `/api/v2/seasons/{seasonId}/groups/{groupId}/billing-settings` | Read group billing settings and own clock-in | Authorized group cashier |
| PUT | `/api/v2/seasons/{seasonId}/groups/{groupId}/billing-settings` | Save group billing settings | Group owner with billing enabled |
| PUT | `/api/v2/seasons/{seasonId}/groups/{groupId}/clock-in` | Set cashier clock-in status | Authorized cashier for self, group owner for clocking out staff |
| PUT | `/api/v2/seasons/{seasonId}/groups/{groupId}/trust` | Save own automatic billing trust | Active season character; own trust only; treasury enabled to opt in |
| POST | `/api/v2/seasons/{seasonId}/groups/{groupId}/bills` | Charge a trusted customer | Active group owner or treasury.bill staff; treasury enabled; active trusted customer |
| GET | `/api/v2/seasons/{seasonId}/groups/{groupId}/bills` | List group bills | Active group records staff or owner; billing staff see only their issued bills; deleted group former owner |
| GET | `/api/v2/seasons/{seasonId}/bills/mine` | List own bills and debt | Current verified character with retained season membership; own bills only |
| GET | `/api/v2/seasons/{seasonId}/bills/{billId}/payments` | Read bill payment receipts | Payer or issuing character; otherwise authorized group records staff or former owner of deleted group |
| GET | `/api/v2/seasons/creation-permissions` | Search verified characters and their global seasons.create permission | Configured provider-verified Lodestone server administrator; no individual season ownership or staff grant implies global authority |
| PUT | `/api/v2/seasons/creation-permissions/{characterId}` | Grant or revoke seasons.create with a reason and current version | Configured provider-verified Lodestone server administrator; no individual season ownership or staff grant implies global authority |
| GET | `/api/v2/seasons/{seasonId}/shop/products` | List shop products | Season member |
| POST | `/api/v2/seasons/{seasonId}/shop/products` | Create a shop listing | Season owner |
| PUT | `/api/v2/seasons/{seasonId}/shop/products/{productId}` | Edit a shop listing | Season owner |
| POST | `/api/v2/seasons/{seasonId}/shop/purchases` | Buy a shop product | Eligible season character or authorized group buyer |
| GET | `/api/v2/seasons/{seasonId}/permits` | Read visible listings and offers | Season member with participant-scoped offers |
| PUT | `/api/v2/seasons/{seasonId}/permits/policy` | Save permit settings | Season owner |
| POST | `/api/v2/seasons/{seasonId}/permits/listings` | Request listing consent | Season permit listing moderator |
| PUT | `/api/v2/seasons/{seasonId}/permits/listings/mine` | Publish or withdraw own listing | Nominated character |
| POST | `/api/v2/seasons/{seasonId}/permits/purchases` | Buy and register a public permit using the displayed price and listing ETag | Owner of the buying group with treasury withdrawal access |
| POST | `/api/v2/seasons/{seasonId}/permits/offers` | Send a private permit offer | Authorized buying group owner |
| PUT | `/api/v2/seasons/{seasonId}/permits/offers/{offerId}/accept` | Accept the private offer and register the permit | Addressed character |
| DELETE | `/api/v2/seasons/{seasonId}/permits/offers/{offerId}` | Withdraw a permit offer | Offer participant |
| DELETE | `/api/v2/seasons/{seasonId}/permits/{permitId}` | Release a registered character | Owning group owner |
| GET | `/api/v2/seasons/{seasonId}/gathering` | Read supply packages and weekly settings | Season member |
| PUT | `/api/v2/seasons/{seasonId}/gathering` | Save weekly gathering and package settings | Season owner |
| POST | `/api/v2/seasons/{seasonId}/gathering/packages` | Produce supply packages | Gathering and Hunting permission |
| GET | `/api/v2/seasons/{seasonId}/classes` | Read class policy and own class selection | Season member |
| PUT | `/api/v2/seasons/{seasonId}/classes/policy` | Set class slots, reset potion price, class grants and group recipe limits | Season owner |
| PUT | `/api/v2/seasons/{seasonId}/classes/selection` | Confirm class choices and pay for a reset potion when required | Season member selecting their own classes |
| GET | `/api/v2/me/moodle-effects` | Read active optional effects and IDs to clear | Authenticated character |
| GET | `/api/v2/seasons/{seasonId}/ownership-effect` | Read the configured ownership debuff | Season owner |
| PUT | `/api/v2/seasons/{seasonId}/ownership-effect` | Set or clear the ownership debuff | Season owner |
| POST | `/api/v2/seasons/{seasonId}/inventory/{holdingId}/use-supplies` | Consume a pack for crafting points | Holding owner with the matching crafting permission |
