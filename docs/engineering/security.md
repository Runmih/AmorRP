# Security and integrity

This describes current controls and their limits. Historical review findings are
condensed in [history](../delivery/history.md); current identity mechanics are in
[authentication](authentication.md).

## Trust boundaries

The plugin is an untrusted client. XIVAuth verifies a stable character binding;
client name/world hints and public IDs confer no authority. Server sessions prove
identity, while current database state proves permissions. Only the configured
administrator receives default global season authority. No first-player bootstrap.

Every verified player has shared season membership. That grants access to ordinary
season data, not another player's writing, private financial history/receipts or
group-private content. Group feature approval, staff grants, season ownership and
global administration are separate. Controls hidden in ImGui also have server checks.

## Commands and financial invariants

Commands use database transactions, advisory locks, UUIDv7 operation keys, request
fingerprints and expected versions. Reauthorize under locks. Same-key replay returns
the protected original receipt only while authorized; a changed request cannot reuse
it. Unknown outcomes keep the original key. Neither retry nor receipt recovery
reposts game chat. Archive guards reject fresh gameplay changes.

Whole-unit arithmetic, constraints and bounded quantities prevent negative,
over-reserved or overflowing accounts. Accounting/claims/reservations settle in one
season transaction. Workers recheck expiry/lifecycle and current state before
mutating. Disband settlement pays the actual owner. Debt never creates spendable
money. Existing tests cover races, replay, scope and migration preservation.

## Hostile input and media

Bounded plain-text fields reject control/bidirectional characters and invalid chat
commands/placeholders. Literal `<3` is preserved. Use preview and committed game
message validation agree. Private bodies and credentials never enter ordinary logs.

StaticImageUpload bounds multipart bytes, source dimensions, animation and decoded
output. It normalizes supported PNG/JPEG/WebP to PNG. MediaAssets shares hashing,
storage, purpose/season-scoped lookup and cleanup; callers retain preflight ownership
checks, locked reauthorization, ETags and receipt ordering. Images use nosniff;
no external image URL becomes a trusted asset.

## Credentials, rate limits and proxy trust

Hash application credentials and provider ownership keys. Encrypt provider grants,
verifiers and stored responses with purpose-bound AES-GCM and retained key IDs.
Protect local sessions, pending commands and personal notes with Windows CurrentUser
DPAPI; preserve origin/character binding. Never copy credentials into diagnostics.
Browser result pages are no-store, frame-blocked and CSP-protected; pairing codes
stay in the browser until the player enters one in the plugin.

Caller/network request limits and bounded login admission protect shared capacity.
Use exactly the pinned Nginx Proxy Manager address as a trusted proxy, one forwarded
hop, and isolated database networking. Never trust arbitrary forwarding headers or
a whole proxy subnet. Renewal uses per-session locks; provider delays can still hold
that session's transaction/connection. Rate limits are not a measured capacity claim.

## Limits and operating evidence

ServicePolicyOptions and SessionPolicyOptions own validated configured bounds;
API capabilities report the public limits. Production keys/credentials/addresses
stay outside source. A database backup without the matching vault keys cannot
restore encrypted state. A deployment is not a restore drill. Retention/deletion,
capacity and private-data export remain explicit readiness work in the
[milestone plan](../planning/README.md). No automatic account-ban or destructive
production processing is added by this maintenance release.
