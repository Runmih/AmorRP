# Expansion beyond 1.0

Future ideas are candidates, not additional 1.0 commitments. Choose features from
actual group testing. Each new type needs creation authority, supply control,
ownership, trade behavior, read/use behavior, migration and interface decisions.

| Candidate | Existing extension point | New work/decision |
| --- | --- | --- |
| Weapons/equipment | Type registry, individual instance payloads | Durability/equip semantics; no real game-stat modification |
| Books/documents | Letter instance and renderer pattern | Chapters, length/storage quotas, edit/lock rules |
| Recipes/ingredients | Production use cases and transactional debit | Multi-resource cost, prerequisites, per-recipe capabilities |
| Containers | Holding/type model | Nested ownership, capacity, cycle prevention, atomic nested trades |
| Per-recipe authorizations | Capability registry + definition ID scope | Grant UX and cost-policy interaction |
| Permission presets/roles | Individual capability grants | Optional convenience presets; preserve underlying grants |
| Group bank | Currency/holding ownership boundary | New owner-kind entity, withdrawal limits, approval history |
| Multiple currencies | Existing currency IDs and ledger | Remove one/group constraint, UI selection, decimal-unit policy |
| Shops/markets | Trades/ledger/domain accounting | Listings, escrow, seller permissions, expiry, abuse handling |
| Public directory | Explicit consent boundary | Discovery design and platform review; never expose private membership |
| Item images/attachments | Group-scoped asset boundary introduced for 1.0 currency icons | Separate owner/content permissions, storage quotas and privacy/cost policy; currency icons alone do not authorize attachments |
| Web/mobile UI | Versioned API/contracts | Separate OAuth clients, browser CSRF/CORS, broader session UX |
| Localization | Centralized label catalog | Translation assets, text measurement, locale testing |
| Horizontal scaling | Persistent sessions/trades/event rows | Shared event fan-out/locks/rate limits; capacity and failure tests |
| Self-hosted deployments | Configurable endpoint, Docker, PostgreSQL | Provider apps per operator, migrations, operator documentation |

## Guardrails against accidental hardcoding

1. Use stable opaque IDs, not names, to link groups/characters/items/currencies.
2. Keep type identifiers as strings with schema versions; registry owns behavior.
   An unknown type renders safely read-only with update guidance.
3. Keep group categories independent of type and authority. New labels do not
   need a binary release, while new behavior does.
4. Persist definition revisions so a new type schema can coexist with old items.
5. Parameterize service limits and expose relevant limits to clients. Avoid
   duplicating 3, 6, 5, or 50 in unrelated UI handlers.
6. Keep weekly policy revisions and quota kinds explicit; do not hardcode reset
   arithmetic throughout endpoints. Changing week boundaries needs migration.
7. Include currency IDs in contracts despite one currency/group initially.
8. Separate provider adapter from domain and service URL from player rules.
9. Keep transport versioning and database migrations separate; support deployed
   older clients during additive changes, and block incompatible writes explicitly.
10. Keep idempotency/reservation/audit patterns reusable across new mutations.

## Avoid premature abstractions

Do not implement a scripting language, arbitrary item plugins, generalized stat
engine, federation, or a universal crafting graph in anticipation of expansion.
Two explicit handlers and a documented registration boundary are sufficient.
Add an assembly/service only when there is an actual dependency or scaling reason.
A clean 1.0 can contain fixed state machines and supported types; the expansion
boundary matters more than making every behavior editable by users.

New-type checklist: specification -> contract/discovery -> schema/migration ->
server handler/authorization/quota -> client renderer/actions -> transaction tests
-> compatibility tests -> security/privacy review -> staged group test.

## M4 writing baseline and extensions

Plain-text `book`, `letter` and `note` now share one written-instance behavior,
private held bodies, permanent author/first-trade lock and one weekly writing quota.
Future rich books should add chapters/pagination via a versioned written schema,
preserving existing instance IDs and old readers. Do not repurpose category names
as behavior, rename every existing LetterId, or give a new subtype a free allowance.

Trading keeps immutable recipe revisions and same-group references. Future extra
currencies require a versioned offer contract/reservation schema and conservation
checks; the present one-currency offer is an explicit 1.0 constraint. Participant
slots and reservations belong in PostgreSQL if adding replicas or notifications.
Scoped polling can be replaced by invalidation events without changing transaction
or confirmation authority. Existing REST queries remain recovery sources.
