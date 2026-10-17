# XIVAuth integration plan

XIVAuth is the selected provider. The maintainer reported the M1 live probe
successful on 2026-10-07. M2 implements the durable flow below, alongside the
isolated probes; see the [M2 guide](../delivery/m2-install-test.md). M2's
`character refresh` scope and live renewal still need explicit M2 acceptance.
No provider client secret or hosted service credential is committed.

The reviewed upstream route source contains an API v1 namespace. Hosted-service
compatibility remains M1 evidence. Source-reviewed probes use authorization-code
with mandatory S256 PKCE, minimal `character` scope, and server-side HTTPS lookup
of `persistent_key`, `lodestone_id`, `name`, `home_world` and `verified_at`.
They do not consume attestations/JWTs or implement signature cryptography.
Record these in the [version matrix](versions.md) and
provider adapter fixtures. Consuming hosted XIVAuth does not require our server
to install or maintain the provider's Rails/Ruby/Redis stack.

## Intended flow

1. Plugin calls Amor RP `POST /api/v2/auth/login-attempts`, giving the selected
   character's display-name/home-world hint. Server creates an expiring attempt,
   state and random attempt credential; hints are not authoritative. The plugin
   supplies a fresh 256-bit `X-Login-Request-Credential`, retained only for retries
   of that start request. This binds unauthenticated response replay to request
   possession; an idempotency key alone must not retrieve login secrets.
2. Server returns a trusted provider authorization URL and attempt credential.
   Plugin asks player to open browser. Provider client secret remains on server.
3. Player authenticates with XIVAuth and consents to verification of the selected
   character, using only the minimal necessary scope.
4. XIVAuth redirects to configured `GET /auth/xivauth/callback`. Server validates
   one-use state, expiry and authorization exchange (PKCE if applicable); obtains
   verified character data and checks selected character, provider, audience,
   expiration and signature as required by the actual provider flow.
5. Plugin polls attempt status using its attempt credential. Status polling does
   not expose session tokens. Browser cannot claim an unrelated plugin attempt.
6. Plugin exchanges successful attempt once for a character-bound session using
   the attempt credential and idempotency key. Refresh/login secrets are not URL
   parameters. Session grants access to current character only.
7. Session renewal rotates refresh credential and revalidates ownership according
   to tested provider capabilities. Local logout revokes the server session and
   closes its socket; remote device revocation is available from session management.

Device authorization is a possible provider-specific substitute behind the same
adapter. Do not implement both for 1.0. Select the supported flow after testing.
Provider/browser login must be usable without another installed Dalamud plugin.

## Identity mapping

- Allocate an internal immutable Character ID. Inventory, currency, membership,
  permissions, quota usage and history refer to that ID, not a name/world string.
  Link it to provider-verified character identifiers through IdentityBinding.
  Record name and home world for display/context matching.
- Never merge on current name or home world. Character rename/transfer updates
  display identity after fresh verification without resetting holdings or quotas.
- A provider user can authenticate different verified characters; their community
  permissions/budgets remain independent. Do not retrieve/store their whole alt list.
- Explicitly test provider character unlink/relink, revocation and transfer to
  another provider account. Never silently transfer old assets to a new binding.
- No Square Enix passwords, one-time passwords, or raw game account IDs are needed.

### Rename and home-world transfer continuity

The M1 source-reviewed XIVAuth response includes `lodestone_id` and `persistent_key`.
Its [ownership-key implementation](https://github.com/XIVAuth/XIVAuth/blob/4bc2440684989cf8e56bc1169afcf5bd3a200172/app/models/character_registration.rb)
derives `persistent_key` from Lodestone character ID, XIVAuth user identity and a
provider secret; name and home world are not inputs. With the same character ID,
provider user and provider key material, a name/world edit does not change that key.
Provider secret rotation or association with another provider account is a different
case and requires an explicit migration/recovery policy.

For a returning verified binding, fetch/revalidate the authorized character using
the stored Lodestone ID and verify the ownership key. The reviewed provider has
`GET /api/v2/characters/{lodestone_id}` for this purpose; a public Lodestone ID
alone never proves ownership. After verified profile refresh, update display name
and home world on the existing internal Character record. Do not create a replacement
record, reset quotas, rewrite asset owners or require joining communities again.
Returning authentication and asset ownership lookups must use verified IDs/key;
do not search by name/world or fall back to them to resolve an existing binding.
Never accept a submitted new name/world as verification or silently skip matching
the logged-in game character. If provider profile data is stale, request refresh and
report the verification delay; preserve every existing asset and binding while it
is resolved. This may require fresh verification, not asset migration or loss.

The M1 probe uses a name/world filter only for its initial temporary discovery.
M2 initial login uses the same discovery hint; saved reauthentication submits
`knownCharacterId`, resolves its stored Lodestone ID and fetches that exact provider
resource. Session renewal also fetches exclusively by stored Lodestone ID. Both
paths verify the ownership key before updating the existing internal Character
record. Community balances and memberships never use name/world lookup.
Provider identifier derivation is documented above. Live paid rename/transfer
tests are waived because of their real-money cost (A16), and no separate simulated
rename/transfer gate is required. Normal authentication tests cover verified
ID/key resolution, correct ownership authorization and reuse of the existing
internal Character record. This does not claim a paid operation was demonstrated.
Other live provider authentication checks remain required.

## Adapter boundary

`ICharacterIdentityProvider` returns normalized verified identity, validity and
renewal information. Provider DTOs/URLs/scopes exist only inside the XIVAuth adapter
and validated options. Domain code sees character identity, not OAuth transport.
Use current maintained OAuth libraries rather than implementing cryptography.
Use a fixed allowlisted issuer configuration, pinned redirects, and validated keys;
no issuer fetched from a submitted token or client-controlled URL.

## Integration gate

| Evidence required | Why |
| --- | --- |
| Developer account/application and callback registration | Hosted service use requires onboarding |
| Minimal scopes and stable verified character claims | Correct ownership mapping and privacy |
| Login in deployed environment | Callback/HTTPS/proxy configuration tested |
| Wrong/unverified character, expired state, callback replay rejected | Spoofing/login takeover prevented |
| Reauthentication/refresh/logout/revocation tested | Sessions do not outlive intended ownership authority |
| Verified ID/key resolves the existing character; wrong ownership rejected; unlink/relink behavior documented | Correct identity/asset authorization; paid rename/transfer tests and a separate simulated-rename gate are not required |
| Provider outage behavior | Existing unexpired sessions may continue; new login/renewal fails clearly |
| Development fixtures isolated from release | No production authentication bypass |

Developer onboarding presently calls for MFA and verified character ownership;
the human maintainer supplies that account access. Application registration and
credentials are external setup dependencies, not information available in this repo.
Review the [developer agreement](https://xivauth.net/legal/devagreement) before beta.
If required stable identity or revocation support cannot be demonstrated, revise
the adapter/session policy and this contract before declaring milestone M1 complete.

## M2 durable storage and lifecycle

Access credentials live for 30 minutes and rotating renewal credentials for 30
days by default; `SessionPolicy` validates configurable bounds. Active use renews
without browser login. Returning after the renewal window, explicit logout,
revocation, lost local credentials or a provider grant failure requires browser
login again. This preserves the character and assets, not a permanent browser grant.

The server hashes Amor credentials and provider ownership keys. Provider refresh
grants, PKCE verifiers and replay responses use .NET AES-GCM envelopes with
purpose binding and key IDs. Keys live in the deployment's secret settings,
separately from PostgreSQL; retain old keys for existing envelopes and backups.
The plugin saves credentials with Windows DPAPI CurrentUser, bound to this machine
and Windows user. It saves renewal and mutation retry keys before sending requests.
There are no permission claims in bearer credentials; authority is read from DB.

OAuth refresh and an Amor database commit cannot be one atomic transaction. A
provider rotation whose response is lost may require fresh browser login; that
cannot transfer or erase community assets. Hosted provider unlink/revocation behavior
and this failure path remain M2 live checks, with further fault recovery at M5.
Numeric home-world IDs are local display hints. A provider world-name update
invalidates a stale numeric hint until fresh selected-character login updates it;
M4 context matching must resolve verified world names through the game world sheet.

## Browser result page — server 0.0.6.1

The durable callback renders a responsive confirmation over the maintainer's
bundled wallpaper, with an opaque dark text panel. On verified success it attempts
`window.close()` after eight seconds; Keep open cancels the timer and Close tab
attempts closure immediately. Browsers may reject closure of tabs opened through
the system browser. In that case the page stays visible with manual-close guidance.
This countdown does not expire the session or control plugin polling/exchange.
Failed, cancelled, invalid and expired callbacks have no automatic closure.

Only fixed local text is rendered, never query parameters, provider error strings,
character details or credentials. Cache-Control remains no-store, Referrer-Policy
is no-referrer and the response uses a per-response CSP nonce for its own inline
script/style. Scripts remove the OAuth query from the current browser-history
entry; no browser storage or external requests are used. Expected callback faults
retain their HTTP status while showing the failure page. API errors retain JSON.

The image is an embedded server resource served by the public content-hashed
`/auth/assets/login-background-{contentHash}.jpg` route with an ETag and immutable
cache headers. It needs no filesystem volume, Reddit connection or remote font.
The HTML/image are included in normal server publish and Docker artifacts.
See the [source map](repository-layout.md) and [deployment guide](../delivery/m5-install-test.md).
