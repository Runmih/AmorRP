# Data model and transaction boundaries

Proposed product schema. M0 has an infrastructure bootstrap migration; M1 identity
probes are memory-only. M2 implements identity/session/group/currency; M3 adds catalog revisions, holdings,
letters, quota snapshots and currency assets. M4 adds written subtypes, persistent
trades, participant slots and offer lines. Full data lifecycle/events remain planned. UUIDs are
opaque server IDs. All timestamps are UTC `timestamptz`. Revision counters are
monotonic integers. Monetary amounts use checked signed `bigint`; positive deltas
create supply, negative deltas destroy supply. Text is bounded by validated service
options, with plain-text storage.

## Entity catalog

| Entity | Key fields and purpose |
| --- | --- |
| Character | Immutable internal ID, current display name, home-world identity, verification timestamp, status; asset ownership remains attached to this ID after rename/transfer |
| IdentityBinding | Character ID, provider namespace, verified Lodestone ID and ownership key/attestation binding; minimum data needed for login, no alt roster |
| LoginAttempt | ID, hashed bootstrap/attempt credential/state, selected character hint, expiry, encrypted short-lived exchange material, status |
| Session | ID, character ID, hashed access/refresh credentials, expiry, refresh family, revoked timestamp |
| Group | ID, owner character ID, name, description, version, deletion timestamp |
| Membership | Group/character unique pair, active/dormant/blocked state, joined timestamps; identity reused on rejoin |
| CapabilityGrant | Group/character/capability unique tuple, granted-by and timestamp; effective only for active member |
| TradeRestriction | Group/character unique tuple, blocked flag, reason, version |
| GroupPolicyRevision | Group, effective period start, potion points, letter count, immutable policy revision |
| Category | Group/ID, name, allowed type IDs, retired timestamp, version |
| ItemDefinition | Group/ID, stable type ID, latest revision, enabled/retired state |
| ItemDefinitionRevision | Group/definition/revision, category ID, name, description, typeDataVersion and typed potion payload |
| Holding | Group/character/ID, type ID, definition revision or instance ID, owned quantity, reserved quantity, version |
| LetterInstance | Group/ID, author ID, title/body, category ID, content version, first-traded timestamp |
| Currency | Group/ID, name, legacy symbol, nullable icon asset ID, version; one/group uniqueness in 1.0 |
| MediaAsset (M3) | Opaque ID, group ID, normalized static PNG bytes, content hash, validated dimensions/length, creator/time; initially currency icons only |
| CurrencyBalance | Group/character/currency unique tuple, owned amount, reserved amount, version |
| CurrencyLedger | Operation/group/currency, affected character, signed delta, reason/transfer counterpart, before/after, actor |
| QuotaUsage | Group/character/kind/period unique tuple, limit snapshot, used, policy revision |
| Invitation | Group/ID, hashed secret, expiry, maximum uses/used, creator, revoked timestamp |
| OwnershipTransfer | Group/ID, current owner, recipient, expiry, proposed/accepted/cancelled status |
| Trade | Group/ID, parties, invited/open/completed/cancelled/expired, revision, expires, confirmed revision per party |
| TradeOfferLine | Trade/party/resource unique line, holding/currency reference and quantity, server-derived safe item preview; own offer revision |
| Reservation | Trade/group/character/resource, quantity/amount, expiry; exact reservation source |
| Operation | ID, group, actor, kind, result resource IDs/versions, timestamp; durable outcome recovery; actor may be null only for anonymous login bootstrap |
| IdempotencyRecord | Actor or login-attempt scope, key, method/path/request hash, operation/result status, replay expiry |
| AuditEntry | Group, actor, action, subject, reason, metadata without letter body/secrets, operation ID |
| ChangeEvent | Group/sequence, recipient visibility, type, resource ID/version, operation ID, timestamp |
| AccountRequest | Character, export/delete request, status, expiry/result location, operator resolution |

Prefer relational fields for ownership, quantities, permissions and cost. A bounded,
schema-validated type payload can hold type-specific description/message fields;
never use unvalidated JSON for core accounting. Type schema versions are explicit.
Asset/currency/trade operations always require an authenticated character actor;
anonymous bootstrap receipts are restricted to their secret-bound login context
and short-lived auth cleanup, never the personal financial history.

## Required constraints and indexes

- Unique verified identity key within provider namespace. Provider values are not
  interchangeable with client-supplied IDs. Separate display names from ownership.
- All character-owned product records reference the internal Character ID. A
  verified rename/world transfer updates display metadata in place without new
  balances, holdings, memberships or quota records. A changed ownership binding
  requires explicit recovery; do not automatically reassign assets by public
  Lodestone ID or a matching name/world.
- Composite uniqueness/FKs enforce group equality between definitions, revisions,
  holdings, letters, currencies, trades and reservations. Application checks alone
  are insufficient to prevent accidental cross-group references.
- `owned >= 0`, `reserved >= 0`, `reserved <= owned`; potion cost > 0; letter quantity
  exactly one; quota `0 <= used <= limit`; invitation uses <= maximum.
- One active membership/group/character; one currency/group; no active trade with
  the same character in another open trade in that group. Model the latter with
  unique active participant slots, not a race-prone pre-insert query.
- Stable stacking key `(group, character, definition, revision)` for potions;
  unique current holding for each letter instance. Dormant holdings still own assets.
- Index membership by character/status, inventory by group/character/type/category,
  history by group/character/time, pending trade participants/expiry, events by
  recipient/group/sequence, quotas by period, cleanup data by expiry.
- Deletion does not cascade blindly through financial history. Tombstone display
  identity and retain bounded non-content audit according to retention policy.

## Atomic operations

| Operation | Records changed in one commit |
| --- | --- |
| Group create/join | Character cap lock, group/membership, defaults/invitation usage, history |
| Ownership accept | Locks on both characters + group; caps, owner, transfer status, history |
| Potion production | Definition/current policy lock, quota debit, holding credit, operation/audit/idempotency/event |
| Letter creation | Quota debit, instance + holding, operation/audit/idempotency/event |
| Consumption/discard/removal | Available quantity decrement, operation/audit/idempotency/event |
| Currency icon replacement/removal (M3) | Same-group media asset/reference, currency version, operation/audit/idempotency; preserve balances |
| Currency adjustment | Available balance, supply ledger, operation/audit/idempotency/event |
| Offer edit | Trade revision, own lines, old/new reservations, confirmations cleared, event |
| Second confirmation | Both inventories/balances, balanced trade ledger, letter locks, release reservations, terminal trade, history |
| Restrict/remove/leave/delete | Membership/restriction/group state, cancellations and reservation release, events |

All implementations authorize inside the command transaction or serialize the
authorization state with it; a preflight permission check outside the transaction
is not enough. Handle serialization/deadlock errors with bounded retries under the
same idempotency key. Recheck expiry on each action. Lock ordering is documented
in code and exercised by concurrent integration tests.

## History and privacy

Personal history shows operations involving that character. Owner history exposes
administrative issuance/removal/grant/policy changes and trade summaries, never letter
bodies. `inventory.remove` exposes only the minimal holding summary needed to act.
Ownership is not automatic letter-reading authority. Trading parties see offered
letter titles/author and may open full contents only after receiving ownership.

Account export includes own identity, memberships, grants, current holdings and
owned letter content, quota usage, and relevant history. It excludes other members'
private content and auth secrets. Deletion requests cancel trades, resolve ownership,
remove credentials/private content, and tombstone necessary shared records; operator
must check exports/backups/retention. Leaving a group is not a data-erasure request.

## Persistence across expansion

Keep currency as an entity despite one/group in 1.0; remove the uniqueness constraint
only with a deliberate multi-currency migration. Definition revisions and letter
instances are distinct so books/equipment can gain their own instance payloads later.
Quota kind is an extensible string; weekly potion/letter mechanics stay separate.
Archived categories and retired definitions remain resolvable for existing holdings.
Do not make group-name or character-name changes rewrite asset identity.

## M2 concrete mapping

`characters` stores the immutable internal ID plus the sole XIVAuth binding
(Lodestone ID unique, ownership-key SHA-256) and mutable profile. A future provider
or explicit ownership recovery adds a reviewed binding migration; no name merge.
`login_attempts`, `sessions` and `used_refresh_tokens` persist login, device renewal
and consumed-token detection. Secrets use purpose-bound vault envelopes; bearer
credentials/state/invitation codes are stored as hashes.

`groups` stores owner, version, deletion state, the single 1.0 currency's stable ID,
name/symbol/version and current/next weekly policy. `memberships` has a composite
(group, character) key, state, individual capability array, trading restriction,
version and single-currency owned/reserved/balanceVersion. Foreign keys enforce
character/group membership references; balance checks enforce 0 <= reserved <= owned.
`invitations` and `ownership_transfers` reference their group and stable recipients.
All capability/restriction changes also have actor/reason/time in `operations`.

`operations` stores scoped UUIDv7 key, request fingerprint (including If-Match),
actor/group/subject, kind, UTC time, reason, currency delta/before/after and an
encrypted response/status/ETag for seven-day replay. A unique scope/key index and
transactional advisory locks enforce one committed effect. Actor receipts remain
recoverable after leaving/deletion, with no private resource replay bypass.

M2 keeps the one-currency balance/policy fields compact. The target catalog is a
1.0 architecture map rather than a claim that every row type is a separate table
already. M3 materializes immutable per-period quota snapshots when spending starts.
A future multiple-currency migration extracts balances keyed by stable CurrencyId;
public IDs, actor ownership and integer-string amounts need no replacement.
Operator retention/purge is an M5 release gate; never reset usage on rejoin.

## M3 currency media mapping

M3 adds a group-scoped asset table for small normalized currency images and a nullable
asset reference beside the existing stable currency fields. Enforce same-group
foreign keys and validated dimensions/size; use PostgreSQL bytes rather than
Render's ephemeral filesystem. Migrate existing M2 currencies without changing IDs
or balances; retain textual symbol fields as legacy compatibility data. Replacing
an icon commits bytes/reference/version and the audited replay receipt atomically.
Retire superseded bytes once no current reference or documented replay/retention
requirement needs them; bound storage and include media in backup/restore checks.
This is a reusable asset boundary, not a general attachment feature in 1.0.

`categories`, `item_definitions` and `definition_revisions` use composite group keys;
`holdings` references same-group membership/category and either definition revision
or letter. Quantities/reservations and distinct payloads have database checks.
`quota_usage` keys group/character/kind/period and stores its limit snapshot/used
count. `groups.InventoryVersion` advances for catalog and inventory writes and
is exposed as list snapshotVersion; updates serialize with membership/currency
through the same group lock. Letter edits change the existing holding name/version.

## M4 actual storage and locking

`trades`: composite (GroupId, Id), participant membership FKs, fixed expiry,
state/revision, both currency amounts, first/final confirmation flags per player,
end reason and completion operation ID. Offer-building increments the revision;
after either first approval an edit cancels. Approvals do not change the revision.
Both first approvals enter `confirming`, both final approvals complete.

`trade_slots`: unique (GroupId, CharacterId), composite trade FK. Either participant
position occupies the same slot; terminal transitions delete both slots.
`trade_lines`: unique (GroupId, TradeId, CharacterId, HoldingId), same-group trade
and holding FKs, positive quantity and safe immutable preview. Writing bodies are
never copied here. Currency reservations are in memberships.Reserved and item
reservations in holdings.Reserved; the trade amounts/lines identify their source.
Only one active trade per participant/group makes those counters unambiguous.

Every asset/trade/membership writer takes the group advisory lock in a database
transaction. Second final confirmation releases reservations and applies both outgoing
sides before incoming sides, checks net capacity/overflow, locks transferred written
instances, sets terminal state and persists one operation receipt/history record.
The operation records TradeId and both parties; the trade retains the complete
published transfer quantities. Currency adjustment records retain delta/before/after.
The conceptual ledger/event catalog above is a future design; M4 has no separate
ledger or change-event table. Supply conservation is verified with PostgreSQL tests.

Written instances remain in `letters` for API/database compatibility, with explicit
TypeId (`book`, `letter`, `note`, default `letter` for M3 rows). Holding payload
constraints permit these three types with quantity at most one and one LetterId.
The TradingBeta migration preserves all existing IDs, content, grants, balances
and quota usage. Server 0.0.6.8 removes its category data rewrites: all existing
names and allowed types remain intact, including legacy Correspondence rows.
New groups seed Books; owners opt legacy categories in explicitly. DoubleTradeConfirmation
adds final flags/state and resets existing pending first approvals while advancing
their revision. Offers, reservations, expiry and terminal history are retained.

### Login origin admission — server 0.0.6.5

login_attempts adds StartedAt and nullable OriginHash (purpose-keyed HMAC of the
normalized originating network). Composite indexes on (OriginHash, ExpiresAt)
and (OriginHash, StartedAt) support shared occupancy and rolling-start counts.
No raw IP is stored for this control. Pending and verified-unredeemed rows occupy
capacity; a SessionId releases it. The auth advisory lock serializes admissions
and transitions across instances. A minute sweep clears fingerprints after
ExpiresAt, without deleting attempts, replay records, identities, sessions or RP
assets. Whole-row retention and backup expiry remain operator responsibilities.
LoginNetworkAdmission retires only old unfinished handoffs without origin data.

### Trade read snapshots — server 0.0.6.7

Read-triggered expiry commits under the existing group lock. DTO assembly follows
in a separate read-only repeatable-read transaction after clearing tracked expiry
entities. The selected trade rows, offer-line text snapshots, participants/authors,
currency ID and holding definition metadata therefore share one PostgreSQL MVCC
snapshot without holding the group write advisory lock. The snapshot ends before
HTTP serialization. Commands still use their original locked write transaction;
no schema or stored data format changes.

### Category policy migration correction — server 0.0.6.8

TradingBeta no longer rewrites category names, allowed-type arrays or versions.
M3 CategoryRow has no seed-provenance marker, and names/IDs alone cannot prove a
row is an untouched default. All existing rows, even Correspondence/Books names,
therefore retain their policy. Fresh groups still seed Books through CatalogService.
Existing letter identity/content/holdings/usage and the schema's added written
subtypes remain intact.

Applied EF migration IDs are not replayed when SQL source changes. An already
upgraded database keeps its current values until owner-authorized repair; the
migration stored no before/after provenance. Compare original owner decisions,
pre-M4 backups or retained operation evidence before editing. Do not infer intended
policy solely from current name, version or allowed-type array.

### Retained group creation accounting — server 0.0.6.9

`groups.CreatedByCharacterId` is a required character FK with its own index. The
server initializes it from the authenticated creator and never changes it during
ownership transfer or soft deletion. Group creation counts all retained rows for
this creator under the existing character advisory lock; default budget is 30.
This resource field does not confer any ownership, membership or access rights.

Migration `20261015201132_RetainedGroupCreationBudget` backfills the earliest valid
`group.create` actor, falling back to current owner where original evidence is
missing. Existing group IDs, owner, versions, assets, memberships and operations
are unchanged. The fallback is accounting only, not inferred historical authority.
Eligible physical group purge frees budget; ordinary soft deletion does not.
Retention/privacy purge must account for this FK as well as other references.
No raw client-provided creator or additional personal identifier is stored.

### Group character profiles — server 0.0.6.10

`memberships.Description` is required text with empty default. Migration
`20261015203517_MemberDescriptions` adds it without changing existing membership,
assets, ownership or grants. It follows the existing character/group key, remains
through leave/rejoin, and is covered by membership retention/purge. Descriptions
are independently editable by their active character per group; no provider or
name-based identity is introduced. Saves use the group/character lock, member ETag
and operation replay, increment member/group versions, and keep text out of audit
summaries. General character identity and XIVAuth records are unchanged.

### Group announcements — server 0.0.6.12

Migration `20261015232913_GroupAnnouncements` adds groups.AnnouncementSequence
(bigint, default zero), memberships.ReadAnnouncementIds (uuid[], empty default),
and announcements (composite group/id key, author FK, sequence, published time,
title and body). A unique group/sequence index orders concurrent publications.
Existing group/member identity, descriptions, assets, balances and grants remain
unchanged. No moderator grants are inferred on upgrade.

Creation serializes on the existing group lock, increments its counter and removes
all but four prior rows before inserting the new message. At most five live rows
remain per group. Author read IDs and explicit reader marks are intersected with
current rows and bounded to five. Reader markers do not change public member
version/ETag; they contain no references that prevent pruning. They survive
leave/rejoin but cannot authorize group access. Counter/read metadata does not
change group profile versions. Create operation replay contains no title/body.
Future physical group/privacy purge must include announcement rows and author FKs.

### Group logos

GroupLogos adds nullable groups.LogoAssetId and LogoVersion=1, with a composite
FK (group ID, asset ID) into media_assets. MediaAsset.Purpose defaults to currency
for existing rows. The database check retains currency's 128px/128KiB bounds and
allows group_logo up to 512px/1MiB. Logo commands use an independent ETag, so
profile/currency versions and identities remain unchanged. One current logo is
retained; replacement/deletion removes its old bytes in the same transaction.
Upgrade preserves all existing assets. Downgrade drops this feature's logos only
before restoring the old media constraint; use backups rather than downgrading if
those new logo bytes must be retained. No downgrade is part of normal deployment.
