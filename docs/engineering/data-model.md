# Data model and transaction boundaries

PostgreSQL is authoritative. EF migrations preserve stable IDs, ownership, assets
and audit receipts. Applied migration files stay immutable. Historical table/column
names such as communities/CommunityId map existing data; the current API uses seasons.

| Area | Persisted state and invariants |
| --- | --- |
| Characters | Stable internal ID, verified Lodestone binding and ownership hash; mutable name/world display fields |
| Authentication | Hashed session/attempt credentials, bounded attempts, consumed refresh tokens, encrypted provider grants/verifiers and replay responses |
| Seasons | Draft/active/archived state; unique active-season index; owner, creator, currency identity, policies and profile versions |
| Season memberships | One row per character/season; only active status is valid; shared membership with independent balances, profiles, grants and read markers |
| Global creation grants | Character key, explicit can-create flag and version; configured administrators derive creation authority separately |
| Currency | Owned/reserved/available whole units, monotonic balance version; no negative or over-reserved account |
| Catalog | Season categories, allowed types, immutable definition revisions and retirement state |
| Inventory | Character-owned holdings, type/provenance/revision, quantity and reservations; holder-only private written content |
| Quotas | Character/season/type-family/weekly-period usage; spent amounts survive grant/profile/session changes |
| Trades | Participants, invitations, offer lines, reservations, revision-bound approval stages and terminal outcomes |
| Groups | Season-scoped owner, membership, approved features, staff permission versions, profile/logo, notices/events and favorites |
| Treasury | Group balance, immutable entries, actual-owner disband returns and overflow claims |
| Billing | Trusted authorization, durable bill identity, debt policy, unpaid claims, payment/collection receipts and continuation state |
| Announcements | Bounded latest-five messages, sequence and per-membership read UUIDs |
| Media | Season-scoped PNG bytes, purpose, dimensions and hash; logo/icon references and monotonic feature versions |
| Operations | Actor/scope/key, request fingerprint, encrypted original response, status/ETag and bounded public metadata |

## Shared membership and lifecycle

New verified characters receive all seasons; new drafts receive all characters.
Discovery/direct reads fill a missing row idempotently. Existing rows are never
replaced by enrollment, so balances, grants, quotas and profile/read state survive.
The shared-membership migration normalizes former inactive/blocked rows and clears
their old grants, then backfills zero-balance rows for missing pairs. Deleted seasons
remain inaccessible. RP-group membership remains independent.

Lifecycle transitions serialize on the lifecycle lock and affected season locks.
Activating a draft archives the current season, cancels its open trades, releases
reservations and freezes its economy before making the draft active. Archived
seasons cannot be reopened. Collection workers skip and recheck inactive seasons.

## Mutation ordering

A service establishes the session, starts a transaction and takes canonical ordered
advisory locks. It reloads mutable ownership/permission state under the lock, checks
authority and operation replay, validates ETags/business invariants, then changes
state and records the protected receipt in the same transaction. Specific services
retain explicit ordering where preflight parsing or discovery is needed.

CommandStore rejects fresh non-lifecycle gameplay writes in inactive seasons.
A durable same-key replay recovers its original receipt; it does not apply the
mutation again. Request fingerprints include method, current path, ETag and body.
Never rewrite a saved request or translate an old API shape during recovery.

CurrencyAccounts, TreasuryLedger, DebtSettlement and TradeReservations own their
respective shared accounting operations. Keep money/claim/reservation updates in
the same transaction and season scope. No new income source bypasses settlement.

Authentication uses short global admission/callback/pairing transactions and
per-session renewal/revocation locks. Provider rotation and database commit are not
an atomic distributed transaction; recovery may require browser sign-in without
changing asset ownership. See [authentication](authentication.md).

## Migration and recovery

Test upgrades against disposable PostgreSQL with preserved historical rows and
current model consistency checks. Never edit old migration SQL to hide a current
mismatch. No schema migration is required for the maintenance refactor. Do not
convert an application downgrade into an automatic database downgrade. Production
restoration uses a matched database backup, vault keys, configuration and image.
