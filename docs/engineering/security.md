# Security and privacy model

This is a design requirement, not an assertion that the prototype is secure.
Threats include modified clients, stolen sessions, cross-community ID substitution,
concurrent duplication, stale trade confirmations, resource exhaustion, and malicious
item text. Community owners intentionally control their fictional economy and alt use.

## Trust boundaries

- Plugin/UI requests are untrusted. Names, world IDs, balance displays, permission
  claims, quota estimates and item IDs are hints until server validation.
- XIVAuth provides authenticated, verified character identity. The integration
  must validate credentials/claims; receiving an identifier alone is insufficient.
- Community ownership grants explicit administrative power, not unrestricted backend
  execution or automatic access to private letter bodies.
- PostgreSQL and server secret configuration are privileged. Game chat is an
  external side effect with no distributed transaction or reliable receipt API.
- No collection of other players' Square Enix account IDs, alt correlation, global
  plugin-user directory, or remote-code execution. Per-character quota is intentional.

## Required controls

| Concern | Required control | Verification |
| --- | --- | --- |
| Spoofed character | Server-controlled XIVAuth exchange; verified stable key bound to session | Tampered/unverified/wrong-character credentials rejected |
| OAuth login interception / shared-link session theft | Exact registered redirects, one-use expiring state, PKCE, attempt credential plus browser-only pairing code | Separate-client shared-link approval cannot redeem; code binding, lockout and replay tests |
| Secret exposure | Provider secret only server-side; redact headers, callbacks, token bodies; OS-protected plugin credential storage | Scan logs/config/package; no credentials in docs/source |
| Session theft | Short-lived opaque access tokens; rotating refresh family; hashes at rest; revoke/logout | Expiry/reuse/revocation tests; sockets terminated |
| Cross-community access | Membership checks plus composite community-scoped constraints for every nested ID | Substitute every resource ID from another community |
| Escalation | Owner-only moderator grants/policies/categories; scoped gameplay delegation; per-command current authority | Gameplay managers can change only Trading/Craft Potions; recipe definition and moderator grants require separate authority |
| Duplication | Transactional accounting, locking, checked arithmetic, durable idempotency | Concurrent creates/consumes/confirms preserve totals |
| Trade race | Revision-specific confirmations; persistent reservations; restrictions serialize with commit | Stale confirmation and restriction/completion races |
| Invalid quantities | Positive bounded quantities, whole monetary strings, overflow checks | Negative/zero/overflow/malformed values rejected |
| Text/command abuse | Plain text; safe unformatted rendering; reject control/line-break/chat command payloads | Unicode/percent/slash/payload/overlong test corpus |
| Discovery/privacy | Only consented own-community rosters; no arbitrary character existence lookup | Nonmember and global lookup probes fail |
| Abuse/exhaustion | Bounded request size, pagination, search length, inventory ceilings, quotas, auth/rate limits | Flood/malformed payload tests with safe failures |
| Unauthorized events | Auth header; community+recipient filtering; periodic session/membership revalidation | Unsubscribe/close on revoke/remove; no private letter payloads |
| Data loss | Paid persistent database, off-provider export, tested restore, migration review | Recovery drill and restart-in-mid-trade tests |
| Dependency compromise | Pinned toolchain/packages, reviewed updates, CI scanning, immutable release artifacts | Reproducible build and source provenance |

Login-start replay also requires the client-generated 256-bit bootstrap request
credential, stored hashed and redacted; an operation key is never authentication.
Encrypted response replay material is access-controlled and purged on expiry or
revocation, including login, refresh and invitation creation secrets.

Opaque random session credentials must have at least 256 bits of entropy; store
cryptographic hashes, not plaintext. Use OS-protected storage on the supported
plugin platform. Access default 30 minutes; refresh default 30 days with provider
revalidation at renewal subject to integration proof. A replayed refresh response
must use its original operation key; unexpected reuse revokes the refresh family.

TLS with trusted certificates and DNS hostnames is mandatory in release builds.
Allow HTTP only in explicitly flagged loopback development. Changing service URL
clears credentials before any request to the new authority. Reject URL credentials,
untrusted redirects and mismatched provider endpoints. Enable browser CSP, secure
cookie settings where used, no permissive CORS, and callback query-log redaction.
Native plugin auth uses bearer headers; browser callback uses OAuth state checks.

## Resource limits (provisional defaults)

| Limit | Initial recommendation |
| --- | --- |
| JSON body | 64 KiB; export responses streamed separately |
| Page size | Default 50; maximum 100 |
| Search term | 100 text elements |
| Community/category/item names | 80 text elements |
| Descriptions/reasons | 2,000 / 500 text elements |
| Letter title/body | 80 / 5,000 text elements |
| Potion use message | 50 text elements plus tested game byte cap |
| Holdings / community definitions / categories | 10,000 per character-community / 1,000 per community / 100 per community |
| Potion quantity per request | 1,000; also bounded by budget/holding ceiling |
| Trade lines | 50 per side |
| Login admission (implemented in server 0.0.6.5) | 5 unfinished attempts/network; 10 new starts/rolling minute/network; 256 unfinished globally |
| Character requests | 120 reads + 30 mutations/minute; bounded event connections |

These are named server options with discoverable public limits. Owner limits
cannot exceed service ceilings. Mutation quotas and community accounting are enforced
durably even if a single-instance ingress rate limiter restarts. Community admission
limits and creation quotas are separate from HTTP abuse limits. Increasing ceilings
requires capacity verification, not an unreviewed owner setting.

## Data policy

Collect verified identity fields needed for community matching, own-community membership,
assets, and necessary operation metadata. No nonessential analytics in 1.0. Explain
character visibility to community members during join; owner sees required administrative
summaries. Letter bodies are private to holder; author attribution remains visible
on the item. Plaintext on-screen privacy is not end-to-end encryption: database
operators can access stored content and the privacy notice must say so.

Provisional retention: login attempts 10 minutes; events 24 hours; response replay
7 days; personal/administrative audit 365 days; deleted-community private content purged
after 30 days; backups follow operator's defined recovery schedule. Rate-limit
metadata must have a short bounded lifetime. Redact item bodies, invitation secrets,
tokens and authentication identifiers from routine logs. Keep correlation/request IDs.

Exports/deletion requests are character-authenticated, with operator resolution in
1.0 rather than a sprawling self-service privacy system. Resolve community ownership
before deletion. Audit identity can be tombstoned; deletion must also address
backup expiry and other holders' received letters. Confirm exact policy before beta.

## Security limits that must be disclosed accurately

Authentication proves authority over a verified character, not that a client is
unmodified or that its user actually posted potion chat. Consent/chat checks in
the legitimate plugin improve behavior, but backend enforcement of actual FFXIV
posting is unavailable. Owner oversight governs RP conduct and alt grants.

No hardcoded production passwords, admin character lists, provider secrets or bypass
headers. Development identity fixtures must be unavailable in release deployments.
Disabling TLS verification or granting all capabilities for debugging is prohibited.

## Implemented M2 controls

Durable product sessions are separate from M1 probe credentials. Current owner,
active membership and individual grants are checked from PostgreSQL on each
command. Nonmember community/foreign member/foreign invitation requests return 404;
a known member without the action gets 403. Character/community DB advisory locks
serialize caps, membership/ownership changes, invite use and currency writes
across server instances. Checked Int64 changes and ledger records are atomic.

All product mutation keys are UUIDv7, request-bound including ETag, age-bounded
on initial submission and replayed from a durable unique scope/key record.
Responses that carry codes or grants are encrypted. Login start replay is bound
to the secret request credential; polling/exchange require the attempt credential.
One-use callback state is committed consumed before a provider call. Refresh
rotation revalidates the exact stored Lodestone ID/key; unexpected reuse revokes
the family. Ordinary credentials are 256-bit random values stored as hashes.

The platform AES-GCM vault uses purpose-bound/versioned envelopes and separate
operator keys with IDs; client saves use Windows DPAPI CurrentUser. Fixed provider
TLS origin, redirect refusal, minimal character+refresh scopes, request/body bounds,
validated text/amounts, required JSON fields and rejected unknown fields constrain
the attack surface. HTTP/provider logging excludes callback/grant data, errors are
safe ProblemDetails, and authenticated/callback responses are no-store. M2 has
server-wide API/login rate budgets; refine actor/network partitioning at M5.

Export/deletion, operator cleanup, detailed abuse/load testing, backup/restore and
release distribution are future gates. OAuth token rotation and DB commit cannot
be atomic; lost provider responses can require reauthentication with assets intact.
See [M2 checks](../delivery/m2-install-test.md) for the hosted renewal gate.


## M3 currency image controls

Currency icons are the sole custom-upload exception in 1.0. Owner changes require
current authority, currency ETag, idempotency and an audit receipt; reads require
current active membership. Accept only bounded decoded static PNG/JPEG/WebP, at
most 128x128, with a recommended advertised 256 KiB encoded-file cap and a separate
bounded multipart body allowance. Reject animations, malformed files, SVG, external
URLs and decoder resource excess. Normalize to PNG and strip metadata using a
maintained decoder with reviewed licensing/deployment compatibility. Persist
normalized bytes in community-scoped PostgreSQL assets, with no public media directory.
Serve from the configured backend with header authentication; private caches are
bounded and cleared on access loss/unload. PostgreSQL tests verify isolation, valid formats, malformed/oversize rejection,
removal and restart persistence. Published-server HTTP checks also cover APNG and
encoded-size rejection plus a valid multipart upload above the JSON ceiling;
live rendering remains in the M3 checklist.

M3 also separates persistent safe command-result feedback from read-refresh status.
Expose sanitized status/code/request ID and operation key for troubleshooting;
never display raw payloads, secrets or private content. A rejected removal produces
no successful community-history event. Do not weaken authorization/version checks to
make a failing interaction appear successful.

Client retry handling preserves a pending mutation when a transport/gateway
failure was followed by a rejection: that rejection cannot prove the first
attempt had no effect. Recovery keeps the same key and never auto-posts chat.
Safe status/code/request IDs remain available without exposing raw payloads.

## M4 implemented trade controls

Session actor and same-community participant/holding/currency checks are mandatory.
Participant-only trade queries return sealed writing summaries and original potion
previews; owner authority does not reveal a member's written body. Terminal trade
and metadata receipt recovery is participant-scoped even after removal. By-key
operation lookup remains actor-scoped. Trade requests reject unknown properties,
repeated lines, invalid decimal amounts, stale revisions and unsupported quantities.

A unique participant-slot key prevents concurrent invitations from bypassing the
one-trade limit. Community-wide PostgreSQL advisory locks serialize reservation, use,
discard, administrative reduction, currency adjustment, expiry, restriction and
completion. Checked int64/int32 arithmetic, net capacity and DB nonnegative/reserved
constraints guard balances/holdings. The atomic completion operation is durably
idempotent; client retries retain the original protected key/request. Recovery never
posts potion chat. Invitations expire from their initial creation time, preventing
indefinite reservations from repeated edits.

Effective M4 defaults (capabilities/validated ServicePolicy): 20 offer lines per
side, 100 copies per creation, 10-minute trade lifetime, 10,000 holdings per
character/community. Earlier provisional recommendation tables are not runtime values.
General ingress remains the current single-instance limiter; more granular abuse
controls and production capacity remain M5 release work.

## Two-stage approval in 0.0.5.2

The server stores one authoritative offer snapshot under its trade ID/revision.
Each authenticated participant must first accept and then approve final review of
that exact revision. Matching opaque client payloads are insufficient: the server
validates ownership, currency, community membership, reservations and quantities.
Approval times may differ within the original fixed expiry (default ten minutes);
there is no simultaneous-click requirement or extension on acceptance. An edit
after either first approval cancels even when its expected revision is stale.
Cancel likewise cannot be blocked by a stale revision. Community locking serializes
an edit/cancel racing the last final approval: cancellation wins before completion,
or the already approved exchange commits first and is immutable. The client never
claims a cancellation succeeded before the server confirms it.

Automatic rebase is limited to unsent offer-building edits where the own-side
baseline is unchanged. A definite stale rejection can be retried with fresh data;
unknown outcomes keep the original protected request/key. Final approval uses the
popup snapshot revision, never a silently substituted latest offer.

## M5 literal message validation

Core owns the shared pure text checks used by server validation and client command
construction. Unpaired `<3`/`>:)` punctuation is permitted; any `<` followed later
by `>` is rejected to avoid game substitution, including party-slot and sound
expressions. Newlines, controls, formatting characters, unsupported UTF-16 and
messages beyond the configured grapheme/200-byte bounds are rejected. The fixed
channel prefix is still client-controlled and authored text is never rewritten
or interpolated. Explicit Use intent, destination checks, one-item commit,
protected-key recovery and no automatic replay chat remain required.

## Review finding 1: shared login link — confirmed and corrected

The pre-0.0.6.2 server trusted possession of an attempt credential after XIVAuth
approval. A caller could initiate for a victim, share the authorization URL, and
redeem the victim’s session after approval in another browser. State/PKCE protect
the provider exchange; they do not establish that the approving browser belongs
to the initiating plugin. A regression test reproduced successful token theft
before the fix using separate HTTP clients.

The corrected callback displays a fresh one-time pairing code only in the
verifying browser. The user must copy it into the plugin they started. The
attempt credential alone, including after status says verified, cannot redeem.
The code has 64 random bits, only a SHA-256 hash is persisted, and a configured
five guesses by default are persisted and serialized by the auth database lock.
The original attempt expiry still applies. Successful redemption/cancellation
clears the hash; lockout clears the provider grant. Same-key redemption replay is
bound to the full code-bearing request hash and session revocation.

Browser HTML is no-store, no-referrer, frame-blocked and nonce-CSP protected, uses
only local assets, strips OAuth query history and does not auto-close while the
code is needed. The page explicitly warns against approving shared login links
or sending the code to anyone. Treat authorization links and pairing codes as
sensitive; logs must redact the code, attempt credentials and token payloads.

This prevents approval of a shared link alone from authorizing the sender’s
plugin. It cannot prevent someone deliberately giving both the approval and
pairing code to an attacker, or compromise of their browser/OS. The explicit
handoff and warning make the device authorization decision visible.

`LoginBrowserPairing` cancels pre-upgrade in-flight attempts and clears their
provider/PKCE secrets. Existing sessions, character IDs and RP assets are retained;
this is prevention, not proof that old sessions were never stolen. If exploitation
is suspected, the operator should use the existing session revocation procedures.
Tests cover the reported attack, absence of the code from polling, wrong-attempt
codes, persistent lockout, expiry/cancellation, safe replay and migration retention.
The live vulnerability remains until server 0.0.6.2 is deployed; publishing a
plugin-only update cannot enforce this on an older server.

## Review finding 2: shared rate buckets — confirmed and corrected

Before server 0.0.6.3, all non-health requests shared the `api` bucket and all
identity routes shared one additional fixed-window bucket. A regression test for
each layer exhausted one network’s budget and showed a different network also
receiving 429. The tests failed on the previous code and pass with caller
partitioning. Both layers now partition by effective IPv4 address / IPv6 /64;
IPv4-mapped IPv6 addresses normalize to the same IPv4 bucket. Health remains
exempt. Outside Render, missing remote-address information uses a conservative
shared fallback; Render mode rejects missing ingress identity instead.

Arbitrary authorization values, names, session IDs or query strings never choose
a limiter key. Server 0.0.6.4 completes the Render integration: `RENDER=true`
automatically selects Cloudflare's overwritten CF-Connecting-IP as the rate key,
following Render's published recommendation. Exactly one valid plain IP is
required; missing/malformed/duplicate headers fail closed with 503, rather than
merging callers into a proxy bucket. Health is exempt. X-Forwarded-For/X-Real-IP
are ignored in this mode, and connection/authentication metadata is unchanged.

This trusts Render's managed public ingress. It does not cryptographically
verify an arbitrary header or protect a direct private route controlled by an
untrusted workspace service. The public URL is the supported ingress for all API
clients; direct-origin access, custom Workers/CDNs or private integrations require
reassessing the boundary. Render documents the overwritten header; local tests
verify application behavior, not the provider's implementation.

Elsewhere, IP headers are ignored unless exact proxies/CIDRs are configured.
ASP.NET forwarding walks trusted hops from the right with a bounded limit; /0
trust is rejected. Render mode and generic proxy lists cannot be combined.

This is network fairness, not proof of individual identity: users behind one NAT
or IPv6 /64 share an allowance. Default application budgets remain configurable
through ServicePolicy. They are process-local and reset on restart; multi-replica
or distributed attacks still need upstream/edge enforcement. Render provides
[upstream DDoS protection](https://render.com/docs/ddos-protection); this correction
retains it and does not provision a custom WAF/rate plan or introduce another
shared application request bucket. No static Render proxy list is required for
the supported public ingress.
See [deployment operations](../delivery/operations.md). Direct public bypass of a
proxy and inappropriate trust ranges undermine network-based limiting.

Regression coverage exercises both rate layers, health exemption, trusted exact
proxy/CIDR paths, forged prefixes/headers, fake bearer values, IPv4 aliases, IPv6
host rotation, empty trust configuration and rejection of wildcard proxy trust.
Render-mode tests additionally cover shared/changing proxy sockets, IPv6 aliases,
missing/invalid/duplicate headers and the complete paired login flow. No live
server change is required to run these tests or continue the review.

## Review finding 3: anonymous global login capacity — confirmed and corrected

Correction 2 partitions HTTP budgets but permits 1200 identity requests per
network/minute, enough for one network to fill 256 pending slots. The regression
expected a network refusal while global room remained and received 201 before
this fix. LoginAdmission now enforces 5 unfinished attempts and 10 new starts per
rolling minute per originating network by default, retaining the global 256 cap.
Pending and browser-verified/unredeemed attempts count. Successful redemption,
cancellation, provider failure and expiry release occupancy; cancellation does
not erase recent starts. Safe idempotent pending-start replay is free.

All admission reads and insertion run within the existing PostgreSQL auth lock
and transaction. State survives restarts and concurrency across server replicas;
it is not an in-memory cookie, character name, bearer string or caller-chosen
credential bucket. ClientNetwork shares the prior fix's trusted Render/direct/
configured-proxy address source and normalized IPv4/IPv6 /64 keys. The origin
controls fairness only; switching networks for browser approval or exchange is
allowed and pairing remains mandatory.

Origin fingerprints use a purpose-derived HMAC with the existing secret vault
key ring, counting retained keys during rotation. The database holds no raw IP
for this feature; fingerprints remain pseudonymous metadata. Expired fingerprints
are cleared by a minute sweep; outages/backups can extend physical retention.
The migration cancels old unfinished attempts with no attributable origin and
preserves saved sessions/assets. Roll out all instances together; see
[operations](../delivery/operations.md). Distinct networks can still fill the
global safety cap, so edge protection and capacity planning remain necessary.

## Review finding 4: history after membership ends — confirmed and corrected

The former history guard accepted any retained membership row, including dormant
or blocked rows, and returned later operations naming that character as subject.
A regression reproduced 200 responses after voluntary departure, owner removal
and blocking, even after a new owner moderation reason was recorded. Previously
documented own-history-after-leaving access had no departure cutoff and therefore
was not a safe historical-receipt boundary.

Server 0.0.6.6 makes both history feeds use CommunityAccess.RequireAsync: current active
membership and a non-deleted community. Audit additionally requires ownership.
Authorization runs before parsing/using page cursors. Departure/removal/block,
unblocking without rejoin and community deletion deny the feed with 404; stored rows
and previous cursor tokens do not grant continuing access. Active personal reads
retain the actor/subject filter; owner audit remains available while authorized.

Recovery stays on existing operation endpoints, with actor/involved-trade-participant
scope and metadata-only responses. Operation-key lookup is actor-scoped. They do
not return moderation reasons or full history entries; being the subject of a
later moderation action alone grants no receipt. Participant-scoped terminal trade
recovery is unchanged. Community history is not a historical feed for former members.
Rejoining as an active member restores the existing personal history scope.

No migration, plugin update or new Render setting is required. Tests cover the
three original bypasses, earlier cursors, unblocking without active rejoin,
retained own receipts by ID/key, rejection of later moderation receipts, active
personal filtering/owner audit and deleted-community access. Saved data is preserved;
no live restart is needed to test or continue review. Deploy the server correction
with the accumulated review batch.

## Review finding 5: trade reads hold the community write lock — confirmed and corrected

List and single-trade reads held an exclusive community advisory lock across response
queries, including per-line lookups. A deterministic regression paused projection
and used pg_try_advisory_xact_lock from another PostgreSQL connection: the community
lock was unavailable on both routes before correction.

Server 0.0.6.7 commits expiry under the community lock, disposes that transaction and
clears the change tracker before beginning a separate read-only repeatable-read
transaction. Page selection and all response fields share that MVCC snapshot;
normal community writes/expiry work do not wait for an advisory lock held by response
projection. The read transaction commits before serializing/sending the response.
This avoids mixing a trade revision from before an edit with lines from after it.
A response may still represent an earlier revision; command-side current-state,
revision, participant and asset checks remain mandatory and unchanged.

TradeService.Views.cs loads four sets per nonempty page: currency, trade lines,
characters/authors and holding definition metadata. Per-trade/per-line database
lookups are removed. Lines retain their original text snapshots, writing bodies
are never loaded, and participant predicates/pagination remain unchanged.
Command responses reuse the batched loader inside their existing locked write
transaction after SaveChanges; write atomicity is retained.

Tests demonstrate a concurrent offer edit completes while both read projections
are paused, with returned revision/quantities from one consistent snapshot.
Additional coverage checks four projection queries for multiple trades/lines,
author/definition metadata, cursor pagination and sealed writing, plus expiry
commit/reservation release even when an invalid cursor or unknown ID rejects the
read. The lock still covers expiry itself; batching expiry or distributed-load
capacity is not claimed by this fix. No new migration, plugin package, dependency
or Render setting is required. Live deployment is deferred with the review batch.

## Review finding 6: category policy broadened during upgrade — confirmed

TradingBeta appended book/note to every category allowing letter, and renamed
matching Correspondence rows. A PostgreSQL upgrade regression reproduced a
letter-only owner policy becoming letter/note/book. Names and allowed types are
owner-controlled; M3 recorded no reliable seed provenance, so limiting the update
to a familiar name would still be unsafe for owner-created lookalikes.

Server 0.0.6.8 removes both data rewrites from the historical migration and
preserves all existing category names/type arrays/versions/retired flags. Fresh
communities still seed Books defaults. Adding a subtype to the schema does not itself
authorize creation in an existing category; that requires an explicit owner edit.
Tests cover custom letter-only, mixed potion/letter, retired, Correspondence/Books
lookalikes and unchanged potion policies, plus original letters/holdings/usage.

This prevents future migration runs; it does not undo already applied changes.
EF tracks migration IDs, and current values alone cannot reliably distinguish
migration effects from later owner choices. No automatic policy rollback is
performed. A read-only operator report and owner-authorized API repair procedure
are documented in [operations](../delivery/operations.md). Existing installations
need owner review using original choices/backups/retained operation evidence.
Tests verify new community defaults, owner-only repair, denied future book/note
creation after narrowing and retained existing written bodies/IDs. No production
policies were inspected or repaired by the build agent; do not label that task
complete merely because source was deployed.

## Resource and approval audit correction — server 0.0.6.9

All three review findings reproduced: repeated create/delete freed active slots
while retaining rows; 32 readiness requests triggered 32 database probes; repeated
first/final approvals with new operation keys recorded redundant consent.

Community creation now serializes an additional retained-record budget on the existing
character lock. `ServicePolicy:MaxRetainedCreatedCommunitiesPerCharacter` defaults to 30,
including active, deleted and transferred communities originally created by that
character. Transfer does not move this accounting. The immutable creator is set
by the server, indexed and backfilled from the earliest recorded `community.create`
actor, falling back to the current owner when no such evidence survives. This
fallback only affects resource accounting, never current ownership or permissions.
The migration preserves records. At capacity creation returns 409
`community_retention_limit_reached`; active ownership/membership allowances remain 3/6.
This bounds retained communities per verified character, not total service storage or
activity across independent characters. Operator retention/purge and upstream
abuse controls remain necessary; do not remove audit evidence merely to free slots.

`ReadinessMonitor` runs one database probe at a time per server process, waits the
configured refresh interval after completion, and atomically publishes its result.
Public `/health/ready` only reads that value and returns no-store 200/503. It stays
outside API rate buckets without allowing request volume to drive database work.
It starts unavailable. The default delay is 5 seconds; the existing probe timeout
is 3 seconds, so loss/recovery detection can lag roughly 8 seconds plus scheduling.
Multiple instances each run their own monitor; this is not an edge traffic limit.

Within the existing trade transaction/lock, a second first-stage or final-stage
approval by the same participant with a new key returns 409
`trade_already_confirmed` without a new operation. Same-key recovery still returns
the original receipt, including after completion. Both participants still approve
the same server revision; the existing expiry/cancel/edit rules are unchanged.

Regression coverage exercises create/delete and ownership-transfer bypasses,
concurrent last-slot creation, original-creator/fallback migration with retained
balances/receipts, both approval stages and same-key recovery, readiness bursts,
and readiness transitions for unavailable/incompatible databases.

## Member description boundary — server 0.0.6.10

The new description PATCH requires an authenticated active membership in a live
community and exact self character ID. Ownership does not permit impersonating another
character's profile. Existing roster/member visibility rules govern reads.
Validation permits at most 300 Unicode text elements, ordinary line breaks and
empty text, rejecting hidden/control characters. Server request-byte caps still
apply. UI renders plain text and never sends this content to chat. ETag/transaction
locking prevents lost saves and replay keeps one operation per key. Audit summaries
omit description content; the ordinary protected response receipt retains the
Member result under existing storage/retention rules.

## Member controls and delegated recipes — server 0.0.6.11

Recipe mutations replace the owner-only check with current owner OR explicit
`items.potion.define` authorization before operation replay. An ordinary crafter
cannot define recipes; a definition-only moderator cannot craft copies or change
categories, community policy, membership or grants. Only the owner grants this action.
The Moderator permissions UI section is organizational, not a role system. Recipe
validation, community scoping, immutable revisions and retirement remain unchanged.

The optional positive trading permission is saved atomically with gameplay grants
under existing locks/ETag/idempotency. Turning it off cancels open trades and
releases reservations without changing ownership proposals; it requires no reason.
Invalid capability sets or stale ETags cannot partly change permissions or cancel
trades. Omission preserves existing trading behavior for older clients.

Membership removal/block/unblock/ownership-proposal buttons require Shift in the
UI and show a short tooltip even when disabled. This reduces accidental clicks;
it is not an authorization mechanism. Existing owner protection, server authority,
required moderation reasons and community boundaries continue to apply.

The new optional trading field is omitted when null during serialization, preserving
the canonical capability-only body used by pre-upgrade operation receipts. An
older pending permission save can recover its original receipt after this update;
changing the trading flag under that same key is rejected as a different request.

## Scoped moderation and announcements — server 0.0.6.12

`community.moderate` permits description/currency-name edits only. Server comparisons
protect community name and legacy currency symbol for nonowners. It grants no gameplay
authority. `members.gameplay.manage` separately allows Trading and items.potion.create
changes through the existing owner/grant endpoint; a nonowner's proposed capability
set must preserve all other current actions. This blocks granting/removing moderator,
recipe-definition, currency-management and inventory-removal authority. Only the
owner can delegate moderator permissions; existing active-member, owner protection,
ETag, locks and idempotency checks remain. Announcement authoring requires the
separate announcements.manage action, checked before command replay.

Announcement title/body are bounded, validated plain text and rendered as literal
text (including title hash characters), never game chat, HTML or format templates.
Community locks serialize create/prune: only the newest five live rows survive, ordered
by a server counter. Read tracking stores at most five UUIDs per membership, with
no audit/operation record per read. It does not churn the member permission ETag.
Create receipts contain only an operation ID, so pruning does not leave message
bodies/titles in response replay or audit summaries. This is live-table retention,
not a claim that backups, logs of operator exports, or previously viewed copies
are erased. Existing backup/purge policies remain applicable.

The authenticated unread endpoint uses two set queries for active joined communities;
the client polls once per 30 seconds (selected list plus counts), including with
the main window closed. Responses are no-store. Notifications preserve UI drafts,
reject results from old character/community scopes, and invalidate stale polls after
read acknowledgements. Badge counts reflect unread retained messages, not every
historical post. Transient poll failure keeps the last known badge state and backs
off. These requests consume ordinary caller-partitioned API budgets.

## Community logos — server 0.0.6.13

GET/PUT/DELETE /api/v2/communities/{communityId}/logo use bearer sessions and active community
membership. Writes are owner-only, checked before hostile-media parsing and again
under the character/community advisory lock; General Moderation grants no logo access.
Current ownership is checked before receipt replay. UUIDv7 keys, original-byte
fingerprints and independent logo If-Match prevent duplicate/stale replacements.

Static PNG/JPG/WebP only: encoded input <=2MiB (operator may lower), source sides
<=2048px (<=4.2MP), no animation, bounded full decode, aspect-preserving downscale
within 512px, metadata-free PNG <=1MiB. Currency keeps its stricter existing limits.
Multipart allowance is raised only on this authorized upload route; JSON limits
remain. No arbitrary remote URL fetch, public asset directory or credential URL.
Read responses are no-store/nosniff; clients stream within image byte budgets.
Purpose-specific DB checks, community-scoped FKs/queries and atomic old-byte cleanup
keep logos separate from currency icons and cap current media storage.
