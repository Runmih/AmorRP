# Authentication and XIVAuth

This describes the implemented durable flow. XIVAuth owns its OAuth endpoints and
`/api/v1/characters` API; Amor RP exposes its own `/api/v2` API. Changing our API
version must never change the provider's URLs. Provider credentials and the registered
callback are deployment settings, not plugin settings or committed secrets.

## Login and pairing

1. The plugin supplies its current character name/home-world hint and a fresh
   random `X-Login-Request-Credential` to `POST /api/v2/auth/login-attempts`, with a
   saved UUIDv7 idempotency key. The server creates an expiring attempt with state,
   an S256 PKCE verifier and a separate attempt credential. Hints do not prove identity.
2. The plugin opens the allowlisted XIVAuth authorization page in the browser.
   The durable adapter requests `character refresh`. The client secret remains on
   the server. The plugin polls with `X-Login-Attempt-Credential` for status only.
3. The callback at `/auth/xivauth/callback` claims one-use state in a short transaction,
   then exchanges the code and fetches verified identity outside that transaction.
   Initial lookup requires exactly one verified character matching the selected
   name/world. Returning identity is bound to Lodestone ID and a hashed ownership key.
4. Completion stores the verified grant and generates a browser-only pairing code.
   The browser displays the code, never Amor RP session tokens. Following a shared
   authorization link does not let its initiator redeem the session without that code.
5. The player enters the code in the plugin. Exchange requires the attempt credential,
   correct pairing code and idempotency key. Guess failures persist and lock the attempt
   at the configured limit. Successful exchange creates one character-bound session.

Attempts default to ten minutes; admission enforces per-network and global capacity
controls. State reuse, expired/canceled attempts, wrong ownership and invalid pairing
are rejected. The result page is bundled HTML plus a content-addressed background
asset; its [asset attribution](../../src/AmorRP.Server/Features/Authentication/Browser/Assets/README.md)
and static content are included in published server files.

## Identity and character context

Inventory, currency, membership, permissions and quotas refer to immutable internal
Character IDs. The server binds those IDs to verified Lodestone identity and the
hash of XIVAuth's `persistent_key`. It does not merge characters by display name or
home world and does not request a provider account's entire alt list.

The plugin selects saved sessions by server origin and the character currently in
game. A character switch clears the previous view and requests; each character
needs its own verified session. Name/world matching is a context safeguard, while
server authorization derives from the credential. A recent world transfer may
require updating the character on XIVAuth before first verification succeeds.
A changed ownership key is rejected rather than silently transferring old assets.

## Renewal, replay and revocation

Access credentials default to 30 minutes; rotating refresh credentials default to
30 days. The plugin saves a renewal key before submission and retries that exact
operation after an uncertain response. The server revalidates identity with XIVAuth,
rotates Amor RP credentials and saves the protected replay response atomically.

Renewal resolves a session ID from either the current credential hash or a retained
used-token row, then acquires that session's PostgreSQL transaction advisory lock.
The lookup before locking supplies only the lock identity; authorization, expiry,
replay and used-token checks are performed again under the lock. Same-key retries
return the recorded result only while it is current and authorized. A different
operation reusing a consumed token revokes the session; a superseded replay cannot
restore older credentials.

Revocation uses the same per-session lock and an actor-scoped lock for receipt
writes. It rechecks the caller after acquiring the locks. Neither renewal nor
revocation holds the global login lock while waiting for a provider response. A
stalled renewal can delay another mutation of that same session, but unrelated
login/renewal/revocation requests remain independent. Provider calls still hold a
connection and that session's transaction; provider timeouts and service capacity
limits continue to matter.

Short login admission, callback claim/completion and pairing transactions retain
the global authentication lock for capacity and identity-binding consistency.
Database locks coordinate server replicas; process-local semaphores would not.

A provider HTTP/timeout failure rolls back renewal and retains the stored session
for retry. Invalid ownership/grants revoke the session. Provider OAuth rotation and
our database commit cannot be one atomic transaction: a lost provider response or
server crash after provider rotation may require fresh browser login. Existing
assets remain attached to the same internal character.

## Secret storage and testing

Amor RP credentials and ownership keys are hashed. Provider refresh grants, PKCE
verifiers and replay responses use purpose-bound AES-GCM envelopes with key IDs.
Deployment keys live separately from the database; retain keys needed by existing
envelopes and backups. The plugin uses Windows CurrentUser DPAPI for sessions,
pending mutations and private notes. Saved credentials/actions remain origin-bound.
Tokens, login links and pairing codes must not appear in ordinary diagnostics.

`FoundationTests`, `LoginPairingTests`, `SessionConcurrencyTests` and provider tests
exercise persistence, concurrent replay/reuse, revocation, stalled renewals, ownership
and pairing with disposable PostgreSQL and injected providers. These do not prove
the availability of the live provider or the correctness of deployment credentials.
Real browser login and renewal remain manual checks after authentication changes;
no second Render service is required for the automated tests.
