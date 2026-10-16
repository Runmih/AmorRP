# Deployment, operations and data recovery

Host: Render web service plus managed PostgreSQL, one region, one initial API
instance. The maintainer reported M1 deployment/login testing passed; this
workspace has no hosted credentials. M2 upgrade configuration is in the [guide](m2-install-test.md). Application and database
remain portable through Docker and standard PostgreSQL.

Follow the [version matrix](../engineering/versions.md). Explicitly provision
PostgreSQL major 18, record its actual hosted minor version, and align local/test
databases with that major. Pin the server's .NET 10 build/runtime images and
digests; rebuild for serviced patches. Record source commit, image digest, SDK,
runtime, driver/provider, database and schema versions for each deployment.
Use compatible PostgreSQL 18 backup tools and repeat restore drills for upgrades.

## Render setup

- Paid web compute for persistent beta/1.0; paid PostgreSQL before users depend on
  holdings. Free server sleeps after 15 idle minutes and free database expires
  after 30 days. Filesystem is ephemeral; do not persist assets or SQLite locally.
- Docker build context must be repository root so Server can reference Core and
  Contracts. Build filters include all three projects and deployment/toolchain files;
  a Server-only root directory would exclude shared files.
- Pre-deploy commands require paid web compute. Free single-instance M3 testing
  uses the opt-in `AMORRP_MIGRATE_ON_START=true` container startup wrapper; leave
  Render's Docker Command empty. It applies pending migrations before starting
  the API and stops on failure. Follow [M3 setup](m3-install-test.md). Keep the
  default unset/false and a separate migration step for paid/scaled deployments.
- Listen on Render's assigned port, trust only configured reverse proxies, expose
  health routes, terminate public TLS using trusted hosting certificates. Use DNS URL.
- Keep database in same region/private network. Restrict public database exposure,
  use dedicated credentials and controlled migration authority.
- Configure exact XIVAuth callback URL for each environment. Separate test and
  production provider app/session keys/data; no copied production user database.
- Secrets: database connection, provider client credentials, encryption/signing keys.
  Nonsecrets: issuer, client ID, callback/base URL, policy ceilings, maintenance state.
- Production root URL may use `onrender.com` initially; custom URL is optional.
  Plugin backend is configurable and credentials scoped to exact backend authority.

Current rough minimum compute budget is around US$13/month for smallest paid API
and Postgres, excluding storage/usage/tax. Larger workloads and a second staging
environment cost more. Recheck current plans before provisioning; no capacity
guarantee follows from that price. Monitor usage, configure available billing
alerts/build limits, and assign a human owner for charges. See [sources](../sources.md).

## Deployment sequence

1. Build/test immutable artifact from reviewed commit; attach matching source/version.
2. Backup/export before destructive migration. Apply reviewed migration in controlled
   step, never as a race between multiple app instances.
3. Additive schema before code requiring it; preserve compatibility with deployed
   clients and previous server binary. Defer destructive cleanup to later release.
4. Deploy, verify readiness/database/version endpoint and authentication callback.
5. Smoke-test login, production, use and a two-character trade in test group.
6. Watch error rate, latency, active reservations, memory, storage and auth failures.
   Roll back code only if schema remains compatible; otherwise restore/forward-fix.

`/health/live` reports process liveness; `/health/ready` verifies required database
and startup migration compatibility. Neither exposes infrastructure/secret details.
XIVAuth temporary outage degrades login/renewal, not process liveness. Maintenance
mode rejects new mutations with a clear message; reads/outcome recovery remain
available where possible. Close/reconnect sockets gracefully during rollout.

## Backups and incident recovery

Paid Render Postgres supports point-in-time recovery; current documented Hobby
window is three days. Maintain an encrypted daily independent logical export with
bounded retention (recommended 30 days) and access controls. Test restoration into
an isolated database before beta, then regularly and after schema changes.

Initial targets: independent-export RPO <=24 hours; restoration RTO <=4 hours.
Measured drills must confirm or revise these targets. Restoring to an older point
can undo completed RP trades; communicate the recovery point and invalidate sessions
to prevent stale command replay. Never silently claim that every later trade survived.

Incident steps: enter maintenance, preserve relevant redacted diagnostics, revoke
compromised sessions/keys as appropriate, determine affected groups, backup current
state, restore/repair in isolation, verify accounting/reservations, publish clear
status to affected users, then reopen mutations. No ad-hoc balance edits without
an audited correction operation.

Monitor: HTTP error/latency, login failure rates, database usage/connections, trade
expiry/reservation age, quota rejection, operation replay mismatch, socket reconnect
rate and storage growth. No letter-body logging. Use configurable cleanup jobs for
retention, stale attempts/exports/events and soft-delete purge. Runtime checks must
enforce expiry even if cleanup is delayed.

## Distribution and support

The maintainer authorized the [custom testing repository](plugin-repository.md)
on 2026-10-15. The main branch serves source plus versioned ZIPs, a generated
manifest, checksums and source/build evidence. Promotion is explicit; ordinary
builds do not update testers. Keep compatibility and recovery instructions current.
Pin supported SDK/toolchain in tracked files. Fresh installation and update must be
tested by a human. Source/license material follows existing AGPL obligations.

An official repository submission is a separate gate. Current Dalamud policies
require human understanding/testing and AI-use disclosure; AI disclosures/submission
text must be human-written. A working custom-distributed 1.0 is not automatically
an approved official plugin. No external maintainer contact is sent by this task.

Before testers: publish operator/support contact, data/privacy notice, retention,
export/deletion route, known limits, outage and maintenance notices. Groups are
closed but do not constitute encrypted private messaging. Owner moderation governs
alt usage and member trading. Assign responsibility for service bills and updates.

## M2 secrets and schema upgrade

Keep the encryption key ring separate from DB backups and include it in secure
recovery instructions. Add keys by ID; never overwrite an old key still referenced
by envelopes/backups. The M2 migration is additive to the M0 bootstrap. Deploy the
reviewed migration through a paid pre-deploy command before the new API; the
current M3 image also supports the free single-instance startup path above.
Probe callbacks and product callbacks have separate options; disable the old
probe surface when it is no longer needed. CLI local native execution can use
`deploy/.env.example` for variable names without storing real values in source.

## M3 inventory/media upgrade

Use [M3 setup](m3-install-test.md) and branch `m3-inventory`. Preserve the database
and existing vault/XIVAuth secrets. Back up before the additive PlayableInventory
migration; deploy migration before serving the new binary. Currency icons use
small normalized PostgreSQL assets and are included in database backups. Old
media bytes are deleted atomically after a successful replacement; receipts
retain identifiers, not historical image content. Restore a matching DB/binary
pair if rollback is needed. Existing Render resources suffice; no new service
or storage account is provisioned by M3.

## Login pairing security deployment — server 0.0.6.2

Deploy the coordinated [0.0.6.6/0.0.6.2 checklist](m5-install-test.md) before
considering review finding 1 fixed in the live service. `LoginBrowserPairing` adds
a hashed browser-only code and persistent guess count, cancelling old unredeemed
attempts while retaining saved sessions/assets. Every server instance must run the
fixed binary. Existing free Render startup migrations remain supported; no paid
Pre-Deploy Command is required. `SessionPolicy__MaxPairingFailures` defaults to 5
(validated range 1–10); `LoginMinutes` still bounds the entire handoff. Do not log
pairing bodies or callback URLs. If old session theft is suspected, use the
existing targeted session-revocation process; this upgrade does not silently
invalidate all users’ saved sessions or prove earlier sessions were uncompromised.

## Caller rate limits and Render ingress — server 0.0.6.4

Render's public web-service ingress passes through Cloudflare and preserves its
`CF-Connecting-IP` header. Render explicitly recommends that header because the
edge overwrites client-supplied values. See [Render's client-IP guidance](https://render.com/articles/host-pocketbase-on-render)
and [Cloudflare header semantics](https://developers.cloudflare.com/fundamentals/reference/http-headers/).
The app automatically enables this mode when Render supplies `RENDER=true`.
**The existing public Render service needs no proxy-IP list, hop count, paid
feature or new environment setting.** Use its public HTTPS address.

The header is trusted only within this managed public-ingress deployment boundary,
not because a header is inherently authentic. Do not expose a separate direct
origin or route untrusted private-network callers to the app port. Workspace
services/operators that can bypass the edge are inside this trust boundary;
private integrations must call the public URL. Other hosting topologies must
explicitly choose their own controlled proxy configuration. Adding a custom CDN,
Worker or private gateway requires reassessing header handling.

In Render mode, exactly one plain IP literal is required on non-health requests.
Missing, duplicate, chained or malformed values return a no-store 503 problem
`client_network_unavailable` before endpoint work. There is no fallback to a shared
proxy bucket or caller-supplied X-Forwarded-For. Rate accounting uses the validated
address without rewriting connection metadata or granting authentication trust.
Health probes remain available without visitor headers. The middleware cannot
independently prove Cloudflare overwrote a header; that guarantee belongs to the
managed ingress, not to the local test harness.

Application settings (one-minute windows, zero queue):

| Setting | Default | Meaning |
| --- | --- | --- |
| `ServicePolicy__ApiRequestsPerMinute` | 6000 | All non-health requests per IPv4 address / IPv6 /64 |
| `ServicePolicy__LoginRequestsPerMinute` | 1200 | Additional identity-route allowance per same client network |
| `ReverseProxy__UseRenderClientIp` | true on Render, false elsewhere | Use the public Render edge's CF-Connecting-IP; do not enable on an arbitrary directly reachable server |
| `ReverseProxy__KnownProxies__0` | unset | Non-Render deployment: exact controlled last-hop proxy IP; add numbered entries as needed |
| `ReverseProxy__KnownNetworks__0` | unset | Non-Render deployment: controlled proxy CIDR; add numbered entries as needed |
| `ReverseProxy__ForwardLimit` | 1 | Non-Render deployment: maximum forwarded hops, validated 1–5 |

Do not combine Render mode with proxy lists; startup rejects that ambiguous
configuration. If proxy-list entries were added while following the earlier
0.0.6.3 instructions, remove those entries for this public Render service. Existing
database, XIVAuth, vault and migration settings stay as documented above.

Outside Render, empty proxy lists use the socket address and ignore IP headers,
including CF-Connecting-IP. Configured ASP.NET forwarding walks trusted hops from
the right and stops at the first untrusted peer or configured limit. /0 trust is
rejected. Do not use a trust-all forwarding switch, guess ranges from outbound
addresses, or take the leftmost caller-controlled X-Forwarded-For value.

Both application budgets use normalized IPv4 / IPv6 /64 keys; fake bearer strings
never mint new buckets. Same-network users share an allowance. Responses use 429,
no-store, Retry-After: 60 and `rate_limited`. Budgets are process-local and reset
on restart. Keep Render's automatic [upstream DDoS protection](https://render.com/docs/ddos-protection).
Multiple replicas require shared upstream application-rate enforcement; these
budgets do not stop distributed/volumetric attacks by themselves.

Local HTTP-pipeline tests cover Render-mode isolation behind shared/changing
proxies, ignored forged forwarding prefixes, invalid-header rejection, IPv6
rotation, direct-host spoof rejection, and the paired login flow. They do not
require a live restart. At the later combined rollout, check readiness, normal
login and authenticated requests through the public URL. Hosted ingress behavior
has not been independently exercised by the build agent. No production setting,
resource or live server was changed. Server fixes take effect only once deployed.

## Anonymous login admission — server 0.0.6.5

Request limits alone do not reserve fair access to pending login storage.
LoginAdmission applies database-backed controls after idempotent replay, within
the same transaction and database-wide auth lock as the attempt insertion.
Defaults are configurable server ceilings; a network means the same normalized
IPv4 address / IPv6 /64 used by the Render-aware HTTP limiter.

| Setting | Default | Enforcement |
| --- | --- | --- |
| `SessionPolicy__MaxPendingLogins` | 256 | Unexpired pending or verified-but-unredeemed attempts globally; range 1–1024 |
| `SessionPolicy__MaxPendingLoginsPerNetwork` | 5 | Same unfinished attempts for one originating network; range 1–64 |
| `SessionPolicy__LoginStartsPerMinutePerNetwork` | 10 | Accepted new starts in the preceding rolling minute, including completed, cancelled or failed attempts; range 1–120 |

Requests above either network limit or the global safety cap receive 429,
`rate_limited`, no-store and Retry-After: 60. These controls are additional to
HTTP rate limits and do not replace edge abuse protection. Failed admission
creates no attempt/replay record. Safe pending-start retries with the same
credential, operation key and body return the original attempt without consuming
admission twice. Browser verification alone does not release the slot; redemption,
cancellation, provider failure or expiry does. Network identity is captured at
start for fairness, not used to bind browser approval/session exchange to an IP.

`20261015191138_LoginNetworkAdmission` adds StartedAt and nullable OriginHash with
indexes, retiring old unfinished handoffs because their originating network
cannot be recovered safely. Saved sessions, characters and RP assets survive.
The existing free Render startup migration path works. Deploy every instance;
old binaries do not enforce these controls and must not run alongside the fix.
No plugin update or new secret is required.

OriginHash is a purpose-derived keyed HMAC using the existing vault key ring,
not a stored raw IP or an unkeyed, enumerable IP hash. It remains pseudonymous
security metadata, not anonymous data. New rows use the active key; admission
counts fingerprints under all retained keys. For rotation, distribute the new
key to every instance before switching active keys; retain old keys while active
attempts or encrypted records require them. Instances must share the same key ring
and policy. Clock synchronization matters for rolling windows and expiry.

LoginOriginCleanupWorker clears expired fingerprints once per minute while the
server is healthy (normally login lifetime plus up to a minute, default about
11 minutes). Outages delay cleanup until a later sweep. Backups can retain older
metadata under the backup policy. This sweep does not purge whole attempt rows,
operation replay or other retained data; existing retention operations still
apply. Do not log request IP headers, fingerprints, callback URLs or credentials.

The global cap deliberately remains: enough distinct networks can still exhaust
it. Same-NAT households and an IPv6 /64 share network budgets; adjust limits only
with capacity evidence. HTTP budgets are process-local, while these admission
checks are shared through PostgreSQL. Tests cover the original exhaustion,
idempotency, cancellation/expiry, browser verification/redemption, rolling-window
restart persistence, key rotation, concurrent starts across two app instances,
address normalization, origin cleanup and migration preservation. No live restart
is needed to test or continue review; the fix takes effect only after deployment.

## History authorization correction — server 0.0.6.6

Group history/audit now require active membership in a non-deleted group; audit
also requires ownership. Membership/asset retention does not authorize ongoing
reads. Departure/removal/block/group deletion returns 404 even with an earlier
cursor; unblocking without rejoining leaves access revoked. Active rejoin restores
normal personal-history scope. Former members recover only their authorized
operation metadata/terminal trade outcomes through the existing separate routes,
without later moderation reasons or private resource replay.

No additional migration, plugin package, secret or Render setting is required.
Retain the previous review migrations. Roll out this server build with the review
batch; no live restart is required for local tests or further review. Existing
own receipt recovery after deletion is covered separately from group history.

## Trade read lock correction — server 0.0.6.7

Trade GETs now hold the group advisory lock only for expiry/save/commit. A fresh
read-only repeatable-read transaction assembles the page or single response after
the lock is released. Projection uses four batched lookups, so query count no
longer grows with each trade/item. Snapshot state stays consistent across concurrent
edits; revision checks on mutations remain authoritative. Invalid read requests
can still commit overdue expiry and release reservations before returning an error.

No schema/migration, plugin package, dependency or configuration change is added.
Existing expiry/command writers keep their transaction/group-lock rules. This
fix does not remove the lock required for expiry itself, nor promise unrestricted
page/line limits or eliminate ordinary database contention. Bounded reads still
consume database/memory resources. Deploy the server correction with the review
batch; no live restart is required for local tests or continued review.

## Category upgrade policy — server 0.0.6.8

The original TradingBeta migration renamed matching Correspondence categories and
added book/note to every category permitting letter, including custom/retired
categories. The corrected historical migration preserves **all existing names,
allowed-type arrays, versions and retired flags**. M3 did not record which rows
were untouched seeds, so a name-based exception is unsafe. Newly created groups
still seed Books (book/letter/note); legacy groups explicitly opt in using owner
category edits. Existing letters/assets/quotas survive the upgrade.

**Already applied migrations do not rerun. Deploying 0.0.6.8 does not restore an
already broadened category.** No automatic shrinking/reversion is added, because
that could override intentional owner decisions made after M4. The migration did
not keep before/after records. Consult original owner choices, pre-M4 backups or
retained operation evidence; incomplete evidence requires owner confirmation.
Do not reset EF history, roll back the production schema, blindly match names/
versions or remove book/note from every current category.

A maintainer with an authorized database connection can run the
[read-only review report](../../deploy/maintenance/review-category-types.sql):

```sh
psql --set ON_ERROR_STOP=1 --file deploy/maintenance/review-category-types.sql
```

Configure the connection locally using normal PostgreSQL environment settings;
never share connection credentials in chat. The report runs in a read-only
transaction, shows whether TradingBeta was applied, and lists current letter
categories that also permit book/note. Legitimate defaults and intentional owner
choices are included. This is a review list, not an automatic affected-row detector;
it does not recover original policy or detect every historical rename. It reads
no letter bodies, login grants or tokens and performs no production changes.

For a confirmed active-category correction, use the **group owner's authenticated
API session**: GET /api/v1/groups/{groupId}/categories/{categoryId}, review the
current name/types and ETag, then PATCH the same route with `CategoryEdit`
(`name`, `allowedTypeIds`), that If-Match and a fresh UUIDv7 Idempotency-Key. Preserve
any intended types/name. Nonowners cannot change policy. The current simplified
plugin has no general category-edit form, so this is an owner-authorized support/
API operation; do not invent a missing UI path or request tokens in chat.
Retired categories already reject new production and cannot be edited through
that API. Owner narrowing blocks new disallowed types while preserving existing
written bodies, holdings and IDs; ordinary operation auditing/versioning applies.

No new schema/migration, plugin package, dependency or Render setting is added.
Existing installations can keep reviewing without restart, but existing policy
repair remains a separate owner-authorized data task. This work does not claim
that those production policies have already been reviewed or repaired.

## Resource controls — server 0.0.6.9

The next combined deployment must apply
`20261015201132_RetainedGroupCreationBudget`. It adds indexed
`groups.CreatedByCharacterId`, recovered from the earliest surviving `group.create`
receipt actor, or current owner if no matching evidence exists. Ownership,
membership, balances, items and receipts are preserved. Update every server
instance with the matching binary; older writers do not populate this column.
No data wipe is part of this deployment.

Optional validated settings have usable defaults:

| Environment setting | Default | Range / meaning |
| --- | --- | --- |
| `ServicePolicy__MaxRetainedCreatedGroupsPerCharacter` | 30 | 1–10000; must be at least `OwnedGroups` |
| `HealthPolicy__ReadinessRefreshSeconds` | 5 | 1–60 seconds after the previous probe finishes |

Retained creation accounting includes soft-deleted and transferred groups and
remains with their original creator. It is independent of the three active-owned /
six active-joined slots. At capacity, creation returns 409
`group_retention_limit_reached`. Deleting frees an active slot, not retained storage;
accepting a transferred group does not consume a retained creation slot. Only an
eligible physical purge under the retention policy frees retained capacity. There
is no automatic purge or reset endpoint in this change. Do not delete creator
receipts, reset EF migration history or alter ownership to bypass the control.

Readiness remains public and exempt from API/network quotas. HTTP requests read a
cached no-store result; one background probe per process retains all connectivity,
migration and schema checks. Startup is unavailable until a probe succeeds. With
the default interval and 3-second probe timeout, readiness may lag a change by
roughly 8 seconds plus scheduling; allow this when diagnosing startup/outages.
Unhealthy probes publish unavailable, then retry on the same schedule. Health
requests cannot initiate probes or cancel the worker. `/health/live` is unchanged.

Repeated first/final trade approvals with new keys now return 409
`trade_already_confirmed`, without duplicate consent records. Replaying the original
operation key still recovers its receipt; it does not cancel the trade.
No Render settings, plugin update, new dependency or immediate live restart are
required to continue the security review. These fixes take effect on deployment.

## Profile update — server 0.0.6.10

Apply `20261015203517_MemberDescriptions` using the existing startup migration
wrapper when deploying the matching server. It adds required membership text with
empty default and retains all existing data. No new environment setting, provider
permission or hosting tier is needed. New plugin 0.0.6.7 description editing needs
this server update; existing 0.0.6.6 clients remain compatible. Descriptions follow
membership retention/purge and protected response receipt retention; the audit
summary does not repeat profile text. No production wipe/deployment was performed.

## Member permission update — server 0.0.6.11

Deploy the matching server before updating to plugin 0.0.6.8 for permission testing.
No new migration or hosting/environment changes are required; apply any earlier
pending migrations normally. New members still default to trading enabled.
Existing stored grants are unchanged: items.potion.define is opt-in and owner-only
to grant. It permits recipe revisions/retirement as well as initial definition.
Disabling trading cancels trades/releases reservations but preserves ownership
proposals. Existing capability requests without tradingAllowed preserve the
current trading setting. No production data wipe/deployment was performed here.

## Group Home deployment — server 0.0.6.12

Apply **20261015232913_GroupAnnouncements** through the existing startup migration
wrapper, then update plugin 0.0.6.9. New table/FKs and empty read markers preserve
existing assets, character identity, member descriptions and grants. No new Render
variables, provider scopes or paid pre-deploy feature is needed. Readiness must
confirm the matching migration set. This task performs no production deploy/wipe.

The live announcement table holds only five messages per group. Create/prune is
transactional and ordered by a server counter. Old message content is absent from
operation receipts/summaries; usual backup/WAL/export retention still applies and
must not be represented as immediate forensic erasure. Read markers stay at five
UUIDs per membership without per-click audit rows. Include announcement author/
group references in future physical purge/export procedures.

Notification polls use normal caller budgets, two reads per 30 seconds per client
(unread counts plus selected list). No persistent connections or extra paid service
is introduced. Read metadata does not affect permission ETags. Grant new actions
explicitly in Members; no previous General Moderation/crafting authority is inferred.

## Home overview deployment — server 0.0.6.13

Deploy main and apply 20261016140911_GroupLogos with the existing
startup wrapper, then update plugin 0.0.6.12. No data wipe, paid Render pre-deploy
command or new external service is needed. MaxGroupLogoBytes defaults to 2097152
(maximum 2MiB); optional ServicePolicy setting can lower it and is advertised in
capabilities. Existing currency media/policies/grants and all identities persist.
Logo edits are owner-only; group.moderate remains description/currency-name only.
Take the normal migration backup; do not run Down as an update step.
