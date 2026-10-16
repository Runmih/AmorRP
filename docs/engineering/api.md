# API contract and endpoint catalog

**Status:** M4 implements authentication, groups, currency/icons, potions, Books
(book/letter/note), quotas, CRUD membership/removal and persistent trading. Events
and data lifecycle remain planned for M5. [OpenAPI](openapi.yaml) declares each
operation's implementation status and is checked against source routes. Capabilities
reports effective limits and supported type IDs. The M5 private usability server and plugin 0.0.6.0
use API v1; package versions and API versions are independent.

## Conventions

- HTTPS, UTF-8 JSON, `/api/v1`. Health and browser OAuth callback are explicitly
  outside that prefix. API version is independent of package semantic version.
- IDs are opaque UUIDs; current character derives from session, never an arbitrary
  body actor ID. Monetary amounts are bounded integer decimal strings, not floats.
- Normal session auth: `Authorization: Bearer <access credential>`. Login attempts
  use `X-Login-Attempt-Credential`. Refresh uses body credential. Tokens never go
  in URLs or normal logs. Browser callback returns HTML with a one-time pairing
  code, never session tokens. Polling returns status only.
- Login start requires a fresh client-generated 256-bit random
  `X-Login-Request-Credential`. It binds start/retry responses so an operation key
  alone cannot retrieve an attempt secret. It proves request possession, not
  character identity. Reuse only for that login start; redact it from logs.
- Every mutation requires `Idempotency-Key` (UUIDv7). Scope is authenticated
  character, or secret-bound bootstrap/attempt/refresh family for auth commands, plus
  method/path, If-Match and canonical payload. Same key/different payload is rejected.
- New keys must have a timestamp within the last 24 hours (bounded future skew).
  Asset-command records replay for seven days; auth replay is additionally bounded
  by attempt/credential validity and revocation. Once response replay expires, reject
  stale key with `operation_replay_expired`; never execute it as a new command.
  Persist key-to-operation identity in retained history and recover by operation ID.
  Replaying an operation still requires current access authorization; revoked users
  may get a receipt via recovery rather than a stale private resource payload.
- Successful mutations return `{operationId, result}` (typed result) or
  `{operationId}`. Response header `Idempotency-Replayed: true` identifies replay;
  clients must not automatically send game chat after replay/outcome recovery.
- Versioned GETs expose ETag; member roster rows also carry their own `etag`
  for member-management preconditions. Conditional edits require `If-Match`; production,
  use/discard/removal and trades submit explicit expected resource revisions in
  their bodies. Missing precondition -> 428; failed ETag -> 412; stale business
  revision -> 409. Creation cannot accept client-selected prices/remaining points.
- Cursor pagination: default 50, maximum 100, stable tie-break by ID. Filters and
  page cursors are group/character-bound. SnapshotVersion allows UI to notice list
  changes; event cursor allows reconciliation. Large export is streamed separately.
- List route matches are more specific than resource-ID routes (`join`, `removals`,
  `potions`, `letters`). Configure routing/UUID constraints deliberately.
- Schema numeric/text bounds reflect the proposed baseline. Effective service
  limits are advertised in capabilities; deployment-specific OpenAPI exports must
  match validated options. Reject unknown request fields for privilege-bearing
  requests; clients tolerate unknown response fields. Type IDs are extensible strings.

## Errors

Use `application/problem+json` with safe `code`, title/status and request ID. Never
send stack traces, SQL, secrets or private letter data. Nonmember resource probes
return 404 without confirming existence; 403 is for a known accessible resource
with an unauthorized action. Common status codes are documented across operations;
not every endpoint will emit every listed error.

| Status | Meaning / representative code |
| --- | --- |
| 400 | Malformed JSON/identifier, `invalid_request` |
| 401 | Missing/expired/revoked credential, `authentication_required` |
| 403 | Action denied, `capability_required`, `trading_restricted` |
| 404 | Unknown or inaccessible resource, `not_found` |
| 409 | `quota_exhausted`, `insufficient_available_quantity`, `group_limit_reached`, `definition_changed`, `trade_changed`, `trade_closed`, `idempotency_mismatch`, `operation_replay_expired`, `inventory_full` |
| 412 / 428 | `version_mismatch` / `precondition_required` |
| 413 / 422 | Body too large / semantically invalid fields, `invalid_quantity`, `invalid_message` |
| 415 | Unsupported image format, `unsupported_media_type` (icon upload) |
| 429 | `rate_limited`; include Retry-After |
| 500 | `internal_error`; safe request ID, recover operation outcome before retry |
| 503 | `maintenance`, `identity_provider_unavailable`, `temporarily_unavailable` |

On timeout the client preserves the same key, checks key/operation/trade status,
and retries only under the original key when appropriate. A 404 lookup may race an
in-flight first request and is not permission to generate a second key. Cleanup,
expiry and restrictions must be enforced transactionally at command time.

## Endpoint catalog

Authorization policies apply in addition to valid session authentication. All group
commands check same-group identity for every referenced resource. Administrative
holdings show summaries only; owner permission does not expose private letter text.
Mutations are marked with an asterisk and require an idempotency key.

### Service

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/health/live` | Process liveness | Public |
| GET | `/health/ready` | Database and startup readiness; failure uses 503 | Public |
| GET | `/api/v1/capabilities` | Versions, maintenance, types, permissions and public limits | Public |

### Authentication

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| POST * | `/api/v1/auth/login-attempts` | Start browser character login | Public bootstrap; secret retry credential; rate limited |
| GET | `/api/v1/auth/login-attempts/{attemptId}` | Poll login status without session secrets | LoginAttempt |
| DELETE * | `/api/v1/auth/login-attempts/{attemptId}` | Cancel own pending login attempt | LoginAttempt |
| POST * | `/api/v1/auth/login-attempts/{attemptId}/exchange` | Redeem with browser pairing code once for session | LoginAttempt + browser-only code |
| GET | `/auth/xivauth/callback` | Registered browser OAuth callback | Exact callback; one-use state; provider exchange |
| GET | `/auth/assets/login-background-{contentHash}.jpg` | Bundled public login wallpaper; immutable content-hashed URL | Public; exact current image hash only |
| POST * | `/api/v1/auth/sessions/refresh` | Rotate refresh credentials and revalidate identity | Valid refresh credential; replay key protected |
| DELETE * | `/api/v1/auth/sessions/current` | Revoke current session and close its event connection | Session |

### Character

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/me` | Current verified character | Session |
| GET | `/api/v1/me/sessions` | Own character sessions | Session |
| DELETE * | `/api/v1/me/sessions/{sessionId}` | Revoke own character session | Session owner |

### Groups

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups` | Current character active groups | Session |
| POST * | `/api/v1/groups` | Create isolated group and defaults within caps | Session |
| POST * | `/api/v1/groups/join` | Accept invitation within membership caps | Session |
| GET | `/api/v1/groups/{groupId}` | Group and own permissions/balance | Active member |
| PATCH * | `/api/v1/groups/{groupId}` | Edit group name/description | Owner; `group.moderate` may edit description only |
| POST * | `/api/v1/groups/{groupId}/leave` | Leave; cancel trades and make holdings dormant | Active member except owner |
| POST * | `/api/v1/groups/{groupId}/deletion` | Soft delete group with exact-name confirmation | Owner |
| GET | `/api/v1/groups/{groupId}/policies` | Current and next weekly policies | Active member |
| PUT * | `/api/v1/groups/{groupId}/policies` | Schedule next-week limits | Owner |

### Membership

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/members` | Consented own-group roster; owner may include dormant members | Active member |
| PUT * | `/api/v1/groups/{groupId}/members/{characterId}/capabilities` | Replace individually granted actions | Owner; `members.gameplay.manage` may change gameplay only; target active nonowner |
| PUT * | `/api/v1/groups/{groupId}/members/{characterId}/trade-restriction` | Restrict/restore both trade directions and cancel affected trades | Owner; active nonowner |
| POST * | `/api/v1/groups/{groupId}/members/{characterId}/removal` | Remove/block member and cancel affected trades | Owner; active nonowner |
| POST * | `/api/v1/groups/{groupId}/members/{characterId}/restoration` | Allow blocked character to rejoin; does not grant a slot or permissions | Owner; blocked member |
| GET | `/api/v1/groups/{groupId}/members/{characterId}/holdings` | Minimal holding summaries for authorized removal; no letter bodies | Owner or inventory.remove; dormant summaries owner only |
| GET | `/api/v1/groups/{groupId}/members/{characterId}/balances` | Balances for authorized currency adjustment | Owner or currency.manage; target active |

| GET | `/api/v1/groups/{groupId}/members/{characterId}` | Current member and ETag | Active member; owner may read inactive |
| PATCH * | `/api/v1/groups/{groupId}/members/{characterId}/description` | Save own group-specific description (300 characters); empty clears | Active member; self only; member If-Match |
| DELETE * | `/api/v1/groups/{groupId}/members/{characterId}` | Leave/remove; preserve assets and allow invited rejoining | Self or owner; owner protected; If-Match |
| PUT * | `/api/v1/groups/{groupId}/members/{characterId}/block` | Block rejoining and cancel affected trades | Owner; nonowner target; If-Match |
| DELETE * | `/api/v1/groups/{groupId}/members/{characterId}/block` | Unblock; leave membership inactive | Owner; blocked nonowner; If-Match |
| PATCH * | `/api/v1/groups/{groupId}/members/{characterId}/holdings/{holdingId}` | Reduce quantity to remainingQuantity; no allowance refund | Self or inventory.remove; expected version; admin reason |
| DELETE * | `/api/v1/groups/{groupId}/members/{characterId}/holdings/{holdingId}` | Remove entire unreserved holding | Same as PATCH; expected version |

### Invitations

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/invitations` | Invitation metadata without secret codes | Owner |
| POST * | `/api/v1/groups/{groupId}/invitations` | Create bounded expiring invitation; return code once | Owner |
| DELETE * | `/api/v1/groups/{groupId}/invitations/{invitationId}` | Revoke invitation | Owner |

### Ownership

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| POST * | `/api/v1/groups/{groupId}/ownership-transfers` | Propose transfer to active member | Owner |
| GET | `/api/v1/groups/{groupId}/ownership-transfers/{transferId}` | Read transfer status | Current owner or proposed recipient |
| POST * | `/api/v1/groups/{groupId}/ownership-transfers/{transferId}/accept` | Accept pending transfer within owner caps | Proposed recipient; active member |
| POST * | `/api/v1/groups/{groupId}/ownership-transfers/{transferId}/cancel` | Cancel pending transfer | Owner or proposed recipient |

### Group logos

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/logo` | Private normalized group logo PNG; 404 when absent | Active member |
| PUT * | `/api/v1/groups/{groupId}/logo` | Upload/replace logo with independent logo ETag | Owner |
| DELETE * | `/api/v1/groups/{groupId}/logo` | Remove logo and restore client placeholder | Owner |

### Currency

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/currency` | Group currency definition | Active member |
| PATCH * | `/api/v1/groups/{groupId}/currency` | Rename currency without replacing identity | Owner or `group.moderate`; symbol stays owner-only |
| GET | `/api/v1/groups/{groupId}/currency/icon` | normalized PNG; default icon when absent | Active member |
| PUT * | `/api/v1/groups/{groupId}/currency/icon` | bounded static image upload/replace; preserve currency identity | Owner |
| DELETE * | `/api/v1/groups/{groupId}/currency/icon` | remove custom image; use bundled default | Owner |
| GET | `/api/v1/groups/{groupId}/balances` | Own available/reserved currency | Active member |
| POST * | `/api/v1/groups/{groupId}/currency/adjustments` | Issue/remove whole-unit currency with reason | Owner or currency.manage; target active |

### Categories

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/categories` | Group categories including retired labels | Active member |
| POST * | `/api/v1/groups/{groupId}/categories` | Create bounded organizational category | Owner |
| GET | `/api/v1/groups/{groupId}/categories/{categoryId}` | Category details and concurrency token | Active member |
| PATCH * | `/api/v1/groups/{groupId}/categories/{categoryId}` | Edit category without changing item behavior | Owner |
| POST * | `/api/v1/groups/{groupId}/categories/{categoryId}/retirement` | Retire category for new content; preserve old holdings | Owner |

### Definitions

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/item-definitions` | Potion definitions with latest revisions | Active member |
| POST * | `/api/v1/groups/{groupId}/item-definitions` | Define potion without issuing copies | Owner or `items.potion.define` |
| GET | `/api/v1/groups/{groupId}/item-definitions/{definitionId}` | Definition revision; latest by default | Active member |
| PATCH * | `/api/v1/groups/{groupId}/item-definitions/{definitionId}` | Create immutable definition revision | Owner or `items.potion.define` |
| POST * | `/api/v1/groups/{groupId}/item-definitions/{definitionId}/retirement` | Retire production without deleting existing copies | Owner or `items.potion.define` |

### Announcements

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/announcements/unread` | Own unread counts across active joined groups | Session |
| GET | `/api/v1/groups/{groupId}/announcements` | Latest five clickable titles and own unread flags | Active member |
| POST * | `/api/v1/groups/{groupId}/announcements` | Create message; physically trim older messages beyond five | Owner or `announcements.manage` |
| GET | `/api/v1/groups/{groupId}/announcements/{announcementId}` | Full retained message; does not mark read | Active member |
| PUT * | `/api/v1/groups/{groupId}/announcements/{announcementId}/read` | Idempotently mark this message read for self | Active member; own metadata |

### Inventory

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/quotas` | Own current usage, limits, reset time | Active member |
| GET | `/api/v1/groups/{groupId}/inventory` | Own holding summaries with filters | Active member |
| GET | `/api/v1/groups/{groupId}/inventory/{holdingId}` | Own holding and type-specific details | Current holder; active member |
| POST * | `/api/v1/groups/{groupId}/inventory/potions` | Spend own potion points to produce copies | Owner or items.potion.create |
| POST * | `/api/v1/groups/{groupId}/inventory/letters` | Spend own letter allowance to create individual letter | Active member |
| GET | `/api/v1/groups/{groupId}/letters/{letterId}` | Read currently owned letter and edit concurrency token | Current holder; active member |
| PATCH * | `/api/v1/groups/{groupId}/letters/{letterId}` | Edit untraded, unreserved letter held by its author | Author and current holder |
| POST * | `/api/v1/groups/{groupId}/inventory/{holdingId}/use` | Consume one available potion and return literal chat text | Current holder; active member; potion type |
| POST * | `/api/v1/groups/{groupId}/inventory/{holdingId}/discard` | Discard own available quantity; no quota refund | Current holder; active member |
| POST * | `/api/v1/groups/{groupId}/inventory/removals` | Remove another active member holding with reason | Owner or inventory.remove; target active |

### Trading

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/trades` | Own trades, pending invitations and recent outcomes | Existing member; own trades and terminal recovery |
| POST * | `/api/v1/groups/{groupId}/trades` | Invite same-group unrestricted character to trade | Active unrestricted member; same for target |
| GET | `/api/v1/groups/{groupId}/trades/{tradeId}` | Read own trade and recover outcome | Participant; terminal status remains available after removal |
| POST * | `/api/v1/groups/{groupId}/trades/{tradeId}/accept` | Recipient accepts unexpired invitation | Recipient; both active/unrestricted |
| PUT * | `/api/v1/groups/{groupId}/trades/{tradeId}/offers/mine` | Replace own offer; reserve resources; cancel if acceptance started | Participant; both active/unrestricted |
| POST * | `/api/v1/groups/{groupId}/trades/{tradeId}/confirm` | Accept exact revision; both approvals open final review | Participant; both active/unrestricted |
| POST * | `/api/v1/groups/{groupId}/trades/{tradeId}/final-confirm` | Approve final review; both final approvals atomically complete | Participant; both active/unrestricted |
| POST * | `/api/v1/groups/{groupId}/trades/{tradeId}/cancel` | Cancel/decline own trade and release reservations | Participant; never undo completed trade |

### History

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/history` | Personal group operation history | Active member of a non-deleted group; own actor/subject entries |
| GET | `/api/v1/groups/{groupId}/audit` | Administrative group history without private content | Owner |

### Recovery

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/operations/{operationId}` | Own operation receipt after ambiguous request | Actor or involved trade participant |
| GET | `/api/v1/operation-keys/{idempotencyKey}` | Own key status; unknown key returns 404 | Authenticated actor only; excludes login/refresh keys |

### Data lifecycle

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| POST * | `/api/v1/me/export-requests` | Request own-character export for operator workflow | Character; rate limited |
| POST * | `/api/v1/me/deletion-requests` | Request deletion; resolve group ownership first | Character; recent reauthentication |
| GET | `/api/v1/me/data-requests/{requestId}` | Read own export/deletion request status | Request owner |
| GET | `/api/v1/me/data-requests/{requestId}/download` | Stream approved unexpired own-character JSON export | Request owner; ready export only |

### Events

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/events` | Upgrade authenticated connection to recipient-filtered WSS events | Session; active group member |

## Representative requests

Headers below omit real credentials. Values are examples, not built-in item IDs.

```http
POST /api/v1/groups/{groupId}/inventory/potions
Authorization: Bearer <character session>
Idempotency-Key: <UUIDv7 generated for this action>
Content-Type: application/json

{"definitionId":"<UUID>","expectedDefinitionRevision":3,"quantity":2}
```

The server computes cost from revision 3 and the current definition; it does not
accept `cost`, `characterId`, or `remainingPoints` from the caller.

```json
{
  "expectedTradeRevision": 7,
  "currencyId": "<UUID>",
  "currencyAmount": "25",
  "items": [{"holdingId": "<UUID>", "quantity": 2}]
}
```

Trade responses enrich item lines with server-derived previews: potion name, exact
revision, description and use message; letter title/author, without its private body.
Recipients never need permission to read another character's entire inventory.
Replace own offer atomically and reserve units during offer-building. Once either
participant accepts, any replacement request cancels the whole trade, including a
stale request. Both `/confirm` approvals open final review without transferring
assets. Both `/final-confirm` approvals for the same revision commit all transfers.
Both approval requests submit only `expectedTradeRevision`. Cancel accepts a stale
revision: cancellation is not approval of an offer.
There is no endpoint to write arbitrary balances, upload executable type behavior,
move items between groups, bypass quota, or directly declare a trade completed.

## WebSocket protocol

`GET /api/v1/events?groupId=<UUID>&afterSequence=<cursor>` upgrades to WSS with the
same bearer header (native client). No query token. One selected-group subscription
per client session. Reject invalid auth/group access before upgrade. 426 without
upgrade; 101 on success. OpenAPI describes the handshake, not a general WebSocket SDK.

Messages are UTF-8 JSON `ChangeEvent` values. Types: `inventory.changed`,
`currency.changed`, `quota.changed`, `trade.changed`, `membership.changed`,
`permissions.changed`, `definitions.changed`, `categories.changed`, `policies.changed`,
`maintenance.changed`, `resync.required`, `session.revoked`. Data carries only
sequence, group, resource ID/version, operation ID and time, not private content.
Recipient filter restricts inventory/currency/quota/trade events; group-wide metadata
is visible only to entitled members. Authoritative data is obtained via HTTP.

Default replay window is 24 hours with a bounded backlog. After a gap/expired cursor,
send `resync.required` and fetch group, holdings, balances, quotas and own open trades.
Use jittered exponential reconnect with cancellation and heartbeat detection.
Duplicate events are harmless; compare sequence/resource version before refresh.
On session expiry/revocation or membership removal close after notification; do not
leave a stale-authorized socket alive. Slow consumers get resync/close, not unbounded
memory buffering. Event delivery never executes a chat action or locally commits a trade.

## Operational actions outside public API

Database migrations, retention cleanup, backup/restore and operator resolution of
data requests use controlled operator workflows, not unauthenticated admin routes.
1.0 has no web-admin interface. Operator export readiness is recorded with an audit
entry; download streams the approved snapshot under owner authentication. Secrets
are never included. Document the verified operator CLI/runbook during implementation.

## Contract maintenance

Implement and export server OpenAPI, compare routes/schemas with this draft, and
keep the catalog synchronized. Integration tests assert authorization/invariants;
OpenAPI validation alone cannot prove those semantics. Add endpoints only alongside
product rules, schemas, failure handling and acceptance coverage.

## M1 temporary feasibility endpoints

These six implemented operations are isolated from the planned durable authentication
API. They cannot authorize groups, inventory or trading. See the [M1 setup guide](../delivery/m1-install-test.md) for lifetime, replay and configuration limits.

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/api/v1/feasibility/login-attempts` | Start temporary XIVAuth feasibility login |
| GET | `/api/v1/feasibility/login-attempts/{attemptId}` | Poll temporary login without session secrets |
| GET | `/auth/xivauth/feasibility-callback` | One-use PKCE feasibility callback |
| POST | `/api/v1/feasibility/login-attempts/{attemptId}/exchange` | Exchange temporary attempt for five-minute probe session |
| GET | `/api/v1/feasibility/identity` | Check temporary authenticated reconnect |
| DELETE | `/api/v1/feasibility/session` | Revoke temporary probe session |

## M2 implementation notes

The [M2 guide](../delivery/m2-install-test.md) records deployment, plugin workflows
and the remaining live renewal check. Login start now includes `homeWorldName`
as a non-authoritative discovery hint and optional `knownCharacterId` for saved
identity reauthentication. Returning lookup and renewal use the stored Lodestone
ID and verified ownership key exclusively. Home-world numeric IDs are display
hints, never proof of identity or authorization.

Group, member, invitation and policy writes lock PostgreSQL character/group
resources in sorted order. Current membership/owner/grants are checked on each
command. Currency updates and before/after ledger records commit atomically.
Operation responses are encrypted; a UUID key alone cannot retrieve login secrets.
After leave/deletion, recover the actor's receipt via operations/key lookup; a
resource replay still requires current access. List cursors are encrypted and
bound to actor, group and list filters. History pages show newest entries first.
M3 has manual/60-second visible-window refresh and no event stream; eventCursor is null.

An older same-key refresh replay is rejected with `refresh_superseded` after a
later renewal; it cannot supply obsolete credentials. A different-key reuse of
an unexpired consumed refresh credential revokes the session family.

## M3 currency icon boundary

The three currency icon routes are implemented in M3 and unavailable in M2. Upload a single
local static PNG, JPEG/JPG or WebP with `multipart/form-data`; no external image URL.
Both dimensions must be at most 128 pixels. A recommended configurable 256 KiB
encoded-file limit is advertised to clients; the route has its own bounded body
allowance including multipart overhead rather than the current JSON ceiling.
Validate actual decoded format and resource bounds, reject animation/malformed
content, strip metadata and normalize to PNG before PostgreSQL persistence.

PUT/DELETE require the currency's current `If-Match` and the same logical mutation's
idempotency key. Fingerprints include the upload content hash and ETag, not unstable
multipart boundaries. Mutations return updated currency plus operation receipt;
update version/reference/audit atomically without replacing currency identity or
changing balances. The optional `iconAssetId` response field is returned by M3.
M2 text symbol fields remain compatible legacy data during migration; release UI
uses custom or bundled icons. Reads require current active membership and use
Authorization headers on the configured backend, with no public media directory.
See [M2 feedback plan](../delivery/m2-feedback-m3-plan.md) for UI and asset lifecycle.

## M4 compatibility and trading wire contract

Ordinary membership deletion is shared by voluntary leaving and owner removal.
Both clear grants, cancel trades and retain assets/usage. Block is a separate
resource; deleting the block restores invitation eligibility, not membership.
M3 `POST /leave`, `POST /removal` and `POST /restoration` remain compatibility
routes. **Legacy `/removal` still blocks**, matching its old button. M4 uses the
canonical routes above. Do not reuse an old operation key on a different path.
Existing own-inventory/balance reads remain aliases; canonical member-specific
reads allow the actor's own assets without an administrative permission.

PATCH a holding with `{expectedHoldingVersion, remainingQuantity, reason?}`;
only reductions are permitted. DELETE takes `{expectedHoldingVersion, reason?}`
and removes the entire holding only if it is unreserved. Administrative reductions
require a reason and active target. The actor comes from the session. Use remains
a separate command because consumption/chat has different behavior from deletion.

A trade invite takes `{recipientCharacterId}`. Accept, confirm, final-confirm and cancel take
`{expectedTradeRevision}`. Offer replacement takes
`{expectedTradeRevision, currencyId, currencyAmount, items:[{holdingId, quantity}]}`.
Trade currency input accepts 1–64 ASCII digits, ignores leading zeros, and must
normalize within nonnegative int64 range. Responses use canonical decimal strings
(e.g. input `04` returns `4`); zero permits item-only trades. One-sided gifts are valid; entirely empty trades are rejected on confirm.
Trade responses contain verified initiator/recipient profiles, immutable item
previews, currency, confirmations, revision, expiry and terminal receipt. No written
body enters a trade response. First trade permanently locks written editing.

Participant trade GETs provide terminal recovery after membership removal. Own
same-key trade command replay remains accessible to its authenticated original
actor; it contains only trade previews, never inventory bodies or credentials.
Receipt GET by operation ID permits the involved trade participant and returns
metadata/resource ID only; by-key lookup remains scoped to its original actor.

M4 uses scoped polling: 2 seconds for the separate window/active trade, 15 seconds for selected
group invitations, 30 seconds after failure, 60 seconds for visible general state.
No WebSocket/event endpoint is advertised as implemented. Persistent expiry sweep
runs every 30 seconds in bounded batches; trade reads/commands also expire due
trades under the group lock. GET may reconcile expiration but cannot transfer assets.

## M5 usability text contract

Capabilities now includes optional/defaulted `definitionNameTextElements` (80)
and `definitionDescriptionTextElements` (300); ServicePolicy configures them.
Recipe names are required single-line plain text; descriptions permit LF newlines
and may be empty. Limits count Unicode text elements, not UTF-16 code units.
Both client preview and server validation share pure Core rules. Chat permits
unpaired `<3`/`>:)` punctuation, rejects paired angle expressions to avoid game
substitution, and preserves the original text. Configured chat character limits
and the central 200-byte ceiling apply through validation and final command
construction. Rejection returns `invalid_message` with a specific explanation;
recipe name/description failures identify the field with `invalid_request`.
No lifecycle route is marked implemented by this checkpoint.

## Plugin 0.0.6.1 Use intent compatibility

The separate startup chat permission UI is removed. The existing API field
`ConsumptionRequest.chatConsent` remains for compatibility with server 0.0.6.0;
the client sends true only when the player clicks Use for the previewed message
and channel. No permission grant is saved or inferred from startup. Existing
replay/recovery responses never trigger automatic chat. The server contract and
schema do not change in this plugin-only patch.

## Browser-to-plugin login pairing — security correction 1

Server 0.0.6.2 / plugin 0.0.6.6 require explicit pairing for every new device login.
`LoginAttemptCreated.pairingRequired` is true for durable login; new clients reject
older servers before opening the browser. Status `verified` means XIVAuth approved
the character, not that the initiating client may redeem its session.

`POST /api/v1/auth/login-attempts/{attemptId}/exchange` requires JSON
`{"pairingCode":"<code copied from the verified browser page>"}`, the attempt
credential header and UUIDv7 operation key. The server creates a random 64-bit
hexadecimal code only after successful verification, stores its SHA-256 hash and
returns its plaintext only in that browser response. No start/status/receipt API
returns it. Keep the exact body and key for a retry; successful replay is body-bound
and cannot be retrieved with a different code. Codes are also attempt-bound,
expire with the attempt, and are cleared after redemption or cancellation.

A wrong code returns 401 `login_pairing_invalid`. By default five failed guesses
persistently fail the attempt with `login_pairing_locked`, including across
restarts; no session is created. `SessionPolicy__MaxPairingFailures` configures
1–10 guesses. Missing JSON/code fails validation; unpaired legacy exchange fails
closed. New login is required after expiry, cancellation or lockout. Refresh of
an existing saved session does not need a pairing code.

## Request throttling — server 0.0.6.4

All non-health routes share one allowance **per client network**, and identity
routes have an additional allowance **per client network**. Effective source is
IPv4 or IPv6 /64. On Render, its managed edge supplies CF-Connecting-IP; elsewhere
the connection IP or explicitly trusted proxy processing applies. Bearer text and
arbitrary forwarding prefixes do not create identities. Render mode requires one
valid plain IP header, otherwise returns a no-store 503 problem with code
`client_network_unavailable` before endpoint work. Defaults are 6000
API / 1200 identity requests per network per minute. 429 responses include
`Retry-After: 60`, no-store and `rate_limited`; health probes remain available.
These limits are local to each server process. Keep edge abuse protection and
use the deployment-specific ingress mode described in
[operations](../delivery/operations.md). Same-network users share an allowance.
There are no new endpoints or wire contracts for this correction.

### Login-start admission — server 0.0.6.5

POST /api/v1/auth/login-attempts additionally enforces durable network admission:
5 unfinished attempts/network, 10 accepted new starts/rolling minute/network and
256 unfinished globally by default (SessionPolicy settings). Pending and
verified-but-unredeemed attempts occupy slots. Safe pending-start replay is free;
completed/cancelled/failed starts still count for one rolling minute. Rejections
use the existing 429 `rate_limited` problem and Retry-After: 60. No new endpoint
or request/response schema is added. Origin accounting is distinct from character
identity and does not restrict where the verified browser completes pairing.

### Group history and personal receipts — server 0.0.6.6

GET /api/v1/groups/{groupId}/history requires an active membership in a
non-deleted group on each request, before cursor processing. A retained dormant
or blocked membership row is not authorization. Voluntary departure, owner
removal, blocking or group deletion makes this feed return 404 `not_found`,
including requests with an earlier cursor. Active users still see only entries
where they are actor/subject; the audit feed additionally requires the active
owner. Unblocking alone leaves membership inactive. An invited active rejoin
restores the ordinary personal-history scope.

There is no post-departure group-history feed. Personal command recovery remains
separate: GET /api/v1/operations/{operationId} returns only operation metadata for
the actor or an involved trade participant; GET /api/v1/operation-keys/{key} is
actor-scoped. Those receipts do not include history summaries/reasons or private
resource payloads. Merely being the subject of a later moderation action does
not grant its receipt. Existing participant-scoped terminal trade recovery is
unchanged. No route or request/response schema is added.

### Trade read transactions — server 0.0.6.7

GET /api/v1/groups/{groupId}/trades and GET .../trades/{tradeId} commit expiry
under the group lock before assembling the response outside it. A fresh read-only
repeatable-read snapshot keeps revision, offers and related metadata consistent
while writes continue. Four set-based projection lookups serve a nonempty page,
independent of trade/line count. The read snapshot ends before serialization.
Invalid cursor/unknown ID responses do not roll back the already committed expiry.

Participant-only scope, terminal recovery, page limits/cursors and wire schemas
are unchanged. Reads represent a snapshot, not an approval of a later server
state; mutation revision and final-confirmation checks still apply. Command
responses keep their existing locked transaction and use the same batched loader.

### Category policy on upgrade — server 0.0.6.8

Existing category names/allowedTypeIds/versions are preserved through M3-to-M4
migration; no owner choice is inferred from a seeded-looking label. New groups
still receive Books defaults. Owners explicitly opt existing categories into
book/note through PATCH /api/v1/groups/{groupId}/categories/{categoryId} with normal
If-Match/idempotency requirements. Narrowing future type permissions preserves
existing written assets. Already-applied migrations do not replay: existing
broadened policies need owner review and explicit repair, not an automatic reset.
No endpoint or wire schema changes.

### Resource and audit controls — server 0.0.6.9

Routes and DTOs are unchanged. Group creation additionally enforces a server
retained-record budget (default 30 groups per original creating character), using
the existing character lock. Active, deleted and transferred creations count;
active 3-owned/6-joined limits are unchanged. At capacity POST /api/v1/groups returns
409 `group_retention_limit_reached`. Same-key retries of an accessible successful
creation still replay the original receipt rather than consuming another slot.

POST confirm/final-confirm with a new key after that participant already approved
returns 409 `trade_already_confirmed` without another consent operation. Same-key
replay still recovers the original receipt under existing authorization rules.

GET /health/ready returns the latest background probe result, initially 503 until
ready, then 200/503, always with Cache-Control no-store. Default refresh is five
seconds after each probe completes, with the existing three-second probe timeout.
It remains public/rate-exempt; HTTP calls perform no database checks. See operations
for optional retained-budget/refresh settings and detection latency.

### Character descriptions — server 0.0.6.10 / plugin 0.0.6.7

`Member` adds an optional `description` response field (empty by default), preserving
older clients. PATCH the member's `/description` subresource with
`MemberDescriptionEdit` (`description`), current member If-Match and UUIDv7
Idempotency-Key. Returns `MemberDescriptionResult`, the usual operationId/result
Member envelope and updated member ETag. Only the authenticated active character
may edit their own profile; owner authority does not bypass this check. Group
membership/deletion access rules also apply before replay. 403
`description_self_only`, 422 `invalid_description`, 412 stale ETag and 428 missing
precondition use ordinary Problem responses. Same-key recovery adds no duplicate
operation. Descriptions are group-specific, multiline plain text, at most 300
Unicode text elements; empty is valid. They are displayed as text, never chat or
markup. Existing member reads and rosters include the description. Member and
group versions increment; audit summaries record the action without copying text.

### Gameplay and recipe-definition permissions — server 0.0.6.11

PUT member capabilities accepts optional nullable `tradingAllowed` alongside the
required capabilities array. True allows trading; false disables it and cancels
open trades/releases reservations in the same transaction. Omitted/null preserves
the existing value for older clients and independent moderator-grant edits. New
members already default to trading allowed. No reason field is needed for this
permission. The existing trade-restriction route remains compatible. Both routes
cancel trades only; gameplay restriction does not cancel ownership proposals.
Owner protection, member ETag, owner-only grant authority, operation replay and
current membership checks remain enforced. Invalid/stale requests change nothing.

Grantable actions add `items.potion.define`. Owner or an active member holding this
action can POST, PATCH and retire group potion definitions. This includes editing
and retiring the group's recipes, but not issuing copies, changing categories or
policies, granting permissions or moderating membership. `items.potion.create`
means Craft Potions and independently requires weekly points and enabled recipes.
Revoking definition authority blocks new commands and old-command response replay.
No endpoint or DB schema is added; capabilities discovery advertises the new action.

The new optional trading field is omitted when null during serialization, preserving
the canonical capability-only body used by pre-upgrade operation receipts. An
older pending permission save can recover its original receipt after this update;
changing the trading flag under that same key is rejected as a different request.

### Group Home and announcements — server 0.0.6.12

Moderator actions are separate: `group.moderate` edits group description and
currency name only; `members.gameplay.manage` changes Trading/Craft Potions only;
`announcements.manage` creates announcements. Only the owner can grant/revoke
these moderator actions. Delegated gameplay saves must preserve every other
capability, including currency, inventory-removal, recipe-definition and moderator
grants. The existing capabilities PUT rejects attempted protected changes with
403 before mutating state. General Moderation cannot rename a group, change the
legacy currency symbol, grant gameplay access, or change policy/invites/icons/
moderation. Existing member/group/currency ETags and current authorization apply.

Announcements use the five routes above. Creation accepts 120-character title and
5000-character multiline body (Unicode text elements, plain text). The writer must
be active and hold announcements.manage or ownership. Create returns a 201 Receipt
containing only operationId; same-key retry is safe. It prunes messages beyond the
latest five in the same group-locked transaction, ordered by a monotonic group
sequence rather than client/server wall-clock ties. No title/body is copied into
operation summaries or protected create receipts. Trimmed IDs return 404; live
DB message content is removed, while ordinary backups/operation metadata follow
operator retention. There is no announcement edit/delete UI in this candidate.

List returns titles/unread flags; full-message GET does not mark read. Read marker
PUT is an explicit, naturally idempotent own-metadata write requiring a UUIDv7
header and current active membership. It returns AnnouncementReadResult directly,
adds no operation/audit row, does not change member ETags and uses no financial
pending-command recovery. Repeating the PUT is safe with the same or a new key.
It retains at most five read IDs per membership, pruning stale IDs on the next
read/post by that character. Authors mark their own new post read automatically.
Read state is per character/group on the server, surviving restarts and clients.
All announcement reads use no-store; unread counts include only active memberships
in nondeleted groups. Removed/blocked/outsider callers cannot read or mark messages.
