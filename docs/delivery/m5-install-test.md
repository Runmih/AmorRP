# M5 private usability build — plugin 0.0.6.12 / server 0.0.6.13

This is the **first M5 test checkpoint**, not the finished milestone or an approved
1.0 release. Plugin is **0.0.6.12**, current server is **0.0.6.13**; minimum client is **0.0.6.6**, branch
**main**, API v1 / Dalamud API 15. The agreed testing series is
0.0.6.x until the release checks and maintainer approval permit 1.0.

## What is implemented in this build

- Literal `<3` potion messages work end to end. Shared Core text rules permit
  unpaired angle punctuation; paired expressions such as `<t>`, `<3>` and `<se.1>`
  remain unsupported to prevent game substitutions. No message is silently rewritten.
- Recipe name/description/message and writing forms show field validation. Recipe
  messages show character/UTF-8 byte counts and an exact preview. Field notes reserve
  their space so validation does not push the rest of the form around.
- Crafting has **Potions**, owner-only **Potion recipes**, and **Books** subtabs.
  Production has a recipe list and selected cost/message/quantity panel; recipe
  editing has a separate list/editor. Permissions and points remain independent.
- Inventory has a resizable list/details layout with Type/Available/Reserved
  columns. Search applies on Enter or focus loss; filter/sort choices apply directly.
  Draft search text is not applied by background refresh. Discard is a separate
  expandable action with the selected item and quantity shown.
- Members has a roster and a separate selected-member panel. Contextual subtabs are
  **Overview**, **Items**, **Permissions** and **Membership**; only authorized actions
  appear. Item summaries load on opening Items. Selected removal item, quantity,
  reason and explicit confirmation stay visible together; no private bodies appear.
- Written bodies have a scrollable read-only view. Editing checks title/body limits,
  stale revisions and trade reservations. New form drafts survive unrelated reads
  and late responses; completed removal/discard clears its confirmation.
- Settings → **General** contains only Show quick launcher. Join, Create group and
  Leave sit beside the group selector; hover boxes and the Compact view button are removed.
  Support remains [GitHub Issues](https://github.com/Runmih/AmorRP/issues).

The accepted main navigation, shared group selector, separate trade window,
two-stage approval/cancel-on-edit, saved login and explicit previewed Use actions
remain the baseline. The 0.0.6.1 correction removes all chat permission prompts.

## Get matching packages

Use the [custom Dalamud testing repository](plugin-repository.md) for installation
and normal updates. The manual package paths below remain a recovery/developer option.

- [Source ZIP, including server and instructions](https://codeload.github.com/Runmih/AmorRP/zip/refs/heads/main).
- [Development branch](https://github.com/Runmih/AmorRP/tree/main).
- [GitHub Actions](https://github.com/Runmih/AmorRP/actions): use a successful
  **Plugin package** run for this branch/version, artifact **AmorRP-plugin**.
  Extract the outer artifact and its inner **latest.zip**.
- A matching successful **Server and contracts** run provides
  **AmorRP-server-linux-x64**, for an operator with the pinned .NET runtime.
  Render normally builds from source using the tracked Dockerfile.

Temporary attachment links are a convenience; the workflow/source links are the
recovery path. The custom testing repository provides permanent versioned packages.

## Deploy the matching server and plugin

Keep the existing database, vault key, XIVAuth application/callback and environment
variables. Take an operator backup before updating. No new app/login/database or
paid hosting setup is required for this private test.

Render caller identification is automatic through its public ingress; no proxy-IP
list or hop-count setting is required. See [operations](operations.md).

1. In the existing Render service select branch **main** and deploy
   manually, using **deploy/Dockerfile** and repository root build context.
2. Keep the free-service setting **AMORRP_MIGRATE_ON_START=true**. Leave paid
   **Pre-Deploy Command** and **Docker Command** overrides blank. The startup wrapper
   checks/applies pending migrations and stops on failure.
3. This build adds **20261015180055_LoginBrowserPairing** and
   **20261015191138_LoginNetworkAdmission**, followed by
   **20261015201132_RetainedGroupCreationBudget**, then
   **20261015203517_MemberDescriptions** and
   **20261015232913_GroupAnnouncements**, after M4 DoubleTradeConfirmation.
   The login migrations cancel old in-flight login attempts lacking pairing/origin data and
   preserve saved sessions and RP assets. Update the plugin to 0.0.6.6 as part
   of the same rollout; see the security-correction checklist below. The group-budget
   migration records each original creator without deleting test or member data.
4. Check `/health/ready` is `ok` and `/api/v1/capabilities` reports
   server **0.0.6.12** and minimum client **0.0.6.6**. New recipe-name/description limits default to 80/300
   text elements. The message default remains 50 text elements / 200 UTF-8 bytes.

Do not erase data for a free Render cold-start timeout. Wait for startup and retry.
The build agent does not deploy to the live service or modify production data.

## Install the plugin

Follow the [repository installation guide](plugin-repository.md) for normal use,
including removing the developer-plugin path while retaining configuration.
The following is the manual developer/recovery method.

Disable Amor RP before replacing files. Extract the complete inner **latest.zip**
into the same developer-plugin folder, then enable it. Preserve the separate
Dalamud plugin configuration so saved login, group and pins survive.

Keep **AmorRP.Core.dll**, **AmorRP.Contracts.dll**, the DPAPI DLL, manifest, deps and
license notices beside **AmorRP.dll**. Core is now shipped for shared pure text rules;
there are no bundled Dalamud/ImGui/game-host DLLs. Verify plugin version **0.0.6.6**.
Open `/amorrp`: no chat permission window should appear. If signed out, click
Log in with XIVAuth; the browser opens, then copy its pairing code into the plugin
and press Finish login. The server
address is fixed to `https://amorrp-server.onrender.com/`, with no input or M1 tools.

## Focused live test

Use the existing test group/characters and small quantities. Existing allowances
are preserved; do not reset data just to create more test items.

1. **Recipe feedback:** owner opens Crafting → Potion recipes → New recipe. Try
   `drinks a potion <3`; it must preview unchanged and save when all fields are valid.
   Try `<t>` or a message over the displayed limit: the reason is shown beside the
   field and saving is unavailable. Correct the input and save. Both a new recipe
   and a revision of an existing recipe should give a visible committed result.
2. **Production/use:** owner and authorized member choose a recipe in Potions.
   Check own remaining points, per-copy/total cost and message. Create one copy if
   allowance permits, select it in Inventory/Consumables and Use once. The count
   drops once and the chosen channel receives the `<3` text. Old recipe copies
   retain their original message. There is no separate permission prompt.
3. **Inventory:** select a long-named item; inspect type, available/reserved count
   and details. Change search, leave its field, change filters/sort and open another
   tab. Wait through background refresh with a draft open. Selection, scroll and
   drafts should remain; no loading/status jump should move the form. Resize the
   two columns and check your normal UI scale.
4. **Writing:** create a book/letter/note within the shared allowance. Try an
   overlong title/body, correct it, create it and read/edit its contents. Its body
   should scroll and be selectable for copying. A received/traded item remains
   sealed against editing, including when returned to its author.
5. **Member removal:** owner selects a member → Items → selects a row. Verify the
   selected name, holder and available/reserved count. Enter quantity/reason and
   confirm; remove one small test quantity. The result must be visible and the
   selected item must be the one affected. Changing members while a read finishes
   must not put the previous member's list into the new panel.
6. **Permissions:** owner opens a nonowner's Permissions subtab. Save/reload a
   grant and verify it from that member's Crafting screen. Nonowners see only their
   authorized controls; owners retain implicit access. Remove, Block and Unblock
   remain distinct actions in Membership.
7. **Short trade regression:** both offer small amounts/items, enter `04`, accept
   then confirm the final popup. Amount is 4 and exchange occurs once. In another
   trade edit after one acceptance: the entire trade cancels and reservations clear.
8. **Startup/support:** reload once, verify retained login/group/pins and that no
   chat permission window appears. If signed out, the idle screen has only the
   login button; clicking it opens XIVAuth. After verification, copy the browser-only
   pairing code into the plugin and press Finish login.
   No server editor or M1 tools should be present. Finish login appears only for
   the explicit pairing step.
   Settings contains only General → Show quick launcher; verify version 0.0.6.6
   in Dalamud’s installer. Use the project’s GitHub Issues page for reports.

Report confusing placement/wording as well as styling. Include version, screen,
steps and sanitized action details. Do not upload tokens, invitation codes or
private text. This checklist is focused new/changed behavior, not a paid rename
or repeated full M3 acceptance run.

## Verification and remaining M5 work

Automated verification now covers 122 server/PostgreSQL tests, 67 plugin/helper tests
and one assembly-boundary test (**190 total**). The new checks cover literal-heart
preservation through production/use, rejected placeholders without changing saved
recipes, configured/discovered limits, exact command construction and existing
trade/recovery behavior. Local Release builds have zero warnings/errors. Locked restores, model parity,
34 Markdown documents/99 API operations, seven startup-wrapper scenarios and the
actual ZIP dependency/host-exclusion check pass. The published Linux server ran
through the migration wrapper on the existing M4 schema; liveness/readiness and
0.0.6.0/recipe-limit discovery passed, and unauthenticated trading returned 401.
Hosted Windows/Linux package checks accompany delivery.
Automation cannot establish actual Dalamud rendering or game-chat delivery.

Export/deletion request processing, maintenance/abuse controls, operating/privacy
policy, backup/restore and capacity drills, installation/update acceptance and
final release review remain M5 work. See the [M5 plan](m5-release-plan.md) and
[acceptance gates](acceptance.md). This build does not declare those complete or
promise production persistence. An unlisted custom testing feed is published; this is not an approved 1.0 release.

Developer commands follow the [M4 guide](m4-install-test.md). Use locked restores,
real disposable PostgreSQL for server tests, the checksum-pinned Dalamud references
for plugin builds, and the actual ZIP checker. Restore without a runtime override,
then publish the server with `-r linux-x64 --self-contained false`.

## Plugin-only cleanup patch — 0.0.6.1

Use existing server 0.0.6.0 and replace the complete plugin package. No new server
deployment, schema migration or XIVAuth registration is required for this patch.
M1 tooling and the chat permission window/checkbox are removed from the plugin.
Configuration version 4 drops the editable backend field; it does not relabel,
clear or forward protected saved sessions or pending commands. Same-origin saved
login/group/pins remain intact. Credentials for old localhost/other origins are
retained under those origins and are never sent to the release server.

The idle signed-out view has one Log in with XIVAuth button. During login, Cancel
and a short status replace it; on failure a concise explanation appears. Browser
URLs still pass the XIVAuth allowlist. Local helper checks pass **62 tests**,
including trusted/untrusted login responses without saved credentials in the
start request. The unchanged server/schema baseline retains its earlier evidence;
full hosted checks accompany the matching patch package.

## Browser confirmation update — server 0.0.6.1

Deploy the latest **main** source to the existing Render service
using the settings above to replace the plain-text XIVAuth confirmation. This is
a **server-only update**: keep plugin 0.0.6.1, database, secrets and callback URL.
There is no schema migration, minimum-client change or need to log everyone out.
The build agent does not deploy the production service.

The supplied wallpaper is included with the server, with a readable dark panel.
A successful verification page attempts to close its tab after eight seconds.
Keep open cancels this; Close tab attempts it immediately. Browser restrictions
may prevent closure, in which case manual-close guidance appears. Failed/expired
attempts stay open. Verification still completes through normal plugin polling.

After deployment, check server version **0.0.6.1**, then use the next ordinary login
to check the image, readability, countdown and retained plugin login. Do not revoke
a working login solely for the cosmetic check. The callback URL may also be opened
without credentials to inspect the styled failure page; that page does not sign in
or start a closing timer. No public success-preview route is added.

Server 0.0.6.1 verification: all **58 PostgreSQL/server tests** pass, including
new callback-page/exchange and failure-nonreflection checks. Headless Chromium
checked the real embedded HTML under its CSP at desktop/mobile sizes, query
cleanup, eight-second closure, blocked-close guidance, Keep open, manual close
and absence of a failure timer. A published Linux binary served the exact supplied
image, embedded HTML, safe headers and conditional image-cache response. Actual
system-browser tab closure remains browser-dependent; it is never required for login.

## Plugin 0.0.6.2 — group-only header and supplied logo

Update through the custom testing repository. The top of the main and compact
windows shows only the group dropdown. Character/currency summaries, connection
buttons, timestamps and routine sync/Ready messages no longer occupy the header.
Settings → Connection retains manual refresh and troubleshooting status.

Action results, unknown-outcome recovery controls and connection failures remain
available in an optional footer below the content. Nothing is drawn there when
idle. Background synchronization never adds rows above the feature content.
Same-key retries and receipt checking still protect unresolved writes.

The installer uses the supplied full logo, resized and padded to a transparent
512 × 512 PNG. It is bundled as icon.png and published at its content-hashed URL.
The original logo is retained in Assets/Branding. It does not replace group coins.

No server update or migration is required for this plugin patch. Check the group
dropdown and absence of idle clutter, Settings → Connection, the installer icon
and one ordinary action's feedback. Actual Dalamud rendering remains a live check.

## Plugin 0.0.6.3 — Join and Create group windows

The shared group dropdown now has **Join** and **Create group** buttons beside it.
They open separate windows containing the corresponding forms, with feedback and
recovery controls. The old Settings forms are removed; membership/leave controls
remain there. Form drafts survive ordinary refresh and reopening. Successful
submission closes/resets its form and selects the joined/created group. Character
changes or sign-out close/reset both forms. Existing group quotas and permissions
still apply.

Update through the same custom repository. No server update or migration is needed.
Test opening each window from the header, closing/reopening an unfinished form,
and an ordinary join/create if needed; verify rejected input stays visible and
success resets the matching form. The two windows have independent size/position.
Allow for the repository host's five-minute cache if the new version does not
appear immediately; fully close/reopen the Plugin Installer to refresh its list.

The same 0.0.6.3 patch removes the main Trades tab. Use the existing in-game
character context menu or Members → Trade with this member to open the separate
trade window; incoming invitations still open it. Settings remains its own
navigation section, including connection controls, history and devices.

## Plugin 0.0.6.4 — two-icon launcher and trade-request badge

Update through the same repository. The small launcher appears while a character
is loaded. The book opens the main window; exchange arrows open trading. Only
icons are shown. A red badge marks pending incoming requests across joined groups;
new requests do **not** open the trade window automatically. Requests are checked
roughly every 15 seconds, including with the main window closed. The badge clears
when requests are accepted, cancelled, expired or no longer accessible.

Move it by dragging the empty padding. Hide/show it under Settings → General →
Show quick launcher, or use `/amorrp launcher`. `/amorrp trade` opens trading
directly. The existing `/amorrp compact` remains the consumables window.

Focused live checks: open each destination with its icon; move the launcher and
check retained position/visibility after reload. With the main window closed,
receive a request from a second tester and wait for the red badge. It must leave
the trade window closed. Click the trade icon, then respond or cancel and verify
the badge clears after the next read. Repeat in another joined group: clicking
its badge selects that group safely, without accepting automatically or interrupting
an existing trade. An expired request must stop showing even without a fresh read.
Signing out or switching character must not carry another character's badge.

The three added helper checks cover cross-group recipient/membership boundaries,
late replies after acceptance/cancellation, expiry and identity changes. No tests
try to duplicate ImGui drawing. No server deployment, migration or extra image is
required; the existing API/schema, server compatibility and secure login remain.

## Plugin 0.0.6.5 — focused settings and automatic trade selection

Plugin-only update; keep server 0.0.6.1 and the current schema. The same custom
repository URL and versioned-package publication process apply.

Focused live checks:

1. Settings shows only General and its Show quick launcher checkbox. There is no
   Connection, Login & devices, History or Support & test build tab, and no Compact
   view button in the vertical navigation. `/amorrp compact` remains available.
2. Header order is group selector, Join, Create group, Leave. As a nonowner, leave
   and confirm the membership disappears. An owner cannot leave before transferring
   ownership; active trades must end first. Ownership offers remain on Home.
3. Hover former result text, field notes, inventory rows and launcher icons: no
   tooltip boxes appear. Inline validation and explicit Action details still work.
4. Complete/cancel an action or trade. Its ordinary feedback disappears after
   10 seconds, or on reopening the main/trade window. An old cancelled/completed
   trade must not reappear on the next poll. An uncertain saved operation must
   retain receipt/retry controls until resolved; do not create a new retry key.
5. Use another character’s in-game Amor RP Trade context action with a shared group
   selected. The member dropdown selects that verified character automatically,
   including when they are beyond the first roster page. Invite is still explicit.
   There is no Find in shared group button. For a character outside this group,
   show feedback and leave Invite disabled; switch to a shared group to retry.
6. Change character/group or choose a different dropdown member while a lookup is
   running. Its late reply must not replace the newly selected target or cross
   the current group/character context. Incoming requests still show a badge only.

Automated verification for this presentation patch: API 15 Release build, existing
65 helper tests, documentation/API parity, and actual package/publication checks.
No server behavior changed, so a new migration or repeated database suite is not
required for this patch. Actual in-game rendering still requires the checks above.

## Security correction 1 — plugin 0.0.6.6 / server 0.0.6.2

The shared-login-link session-theft finding was reproduced and is valid. This is
an authentication protocol correction, unlike the earlier plugin-only UI patches.
Update the plugin through the same custom repository, then manually deploy the
matching server source on Render. Existing saved sessions continue to renew;
new login now requires copying a browser-only pairing code into the plugin.

Deployment:

1. Update/install plugin 0.0.6.6. New clients refuse a fresh login through an older
   server rather than falling back to automatic unpaired redemption.
2. Deploy branch `main` to the existing Render service. Keep the
   existing database, XIVAuth app/callback and vault keys. For the existing free
   service retain `AMORRP_MIGRATE_ON_START=true`, blank Pre-Deploy/Docker Command,
   and let the startup wrapper apply `20261015180055_LoginBrowserPairing`.
   No paid command or new provider application is needed.
3. Verify `/health/ready` and `/api/v1/capabilities`: server 0.0.6.2, minimum client
   0.0.6.6. Complete the rollout to all instances; an old server remains vulnerable.
4. Migration cancels only old in-flight login attempts. Existing saved sessions,
   groups, inventories and currency remain. Start a new login if one was open
   during deployment. Keep the security-fixed server; do not roll back to the
   vulnerable binary as an authentication recovery step.
5. For a test character/device without a saved login: approve XIVAuth, copy the
   16-character pairing code from the browser to the plugin, press Finish login.
   The browser stays open so its code remains available; Close tab is manual.
   Never share a login link or pairing code. Normal saved-login restart remains
   automatic and does not ask for the code again.

Focused regression evidence: the attack regression test failed on the vulnerable
code and passes after correction (the original sender
received tokens before the fix; it now receives a rejection). The suite also
checks code-free redemption, code secrecy/binding, expiry/cancellation, persistent
lockout, single-use/same-key retry, saved-session migration retention and the
plugin’s refusal to auto-exchange on browser approval. See
[security model](../engineering/security.md) and [API handoff](../engineering/api.md).

## Security correction 2 — server 0.0.6.4, plugin stays 0.0.6.6

The API and identity limiter previously used one bucket each for all callers.
Both cross-client exhaustion cases were reproduced and now use separate budgets
per IPv4 address or IPv6 /64. Fake bearer values and untrusted forwarding headers
cannot select a bucket. This is server-only: no new migration, contract or plugin
package beyond correction 1. Existing API/login limits remain 6000/1200 requests
per minute by default, now per client network.

Server 0.0.6.4 completes Render support by automatically using its edge-overwritten
CF-Connecting-IP header when RENDER=true. Forwarding prefixes and fake bearer
values do not choose buckets. Missing/invalid/duplicate identity headers return
503 rather than sharing a proxy bucket. No static proxy lists or hop counts are
needed for the public Render service. See [Render ingress operations](operations.md).

No live restart is needed to continue the review or run these tests. Batch the
server fixes in one later deployment. During that rollout verify readiness and
normal login/API requests through the public URL. Local pipeline tests do not
independently verify the provider's header overwrite; that behavior is documented
by Render. No public IP-debugging endpoint is added, and no production settings
were changed. Render's existing upstream DDoS protection remains in place.

## Security correction 3 — server 0.0.6.5, plugin stays 0.0.6.6

The per-network HTTP limits from correction 2 still allowed 256 anonymous starts,
filling the global login cap. A regression reproduced this before the fix.
New durable admission limits default to **5 unfinished attempts per network** and
**10 new starts per rolling minute per network**, alongside the **256 global cap**.
Both pending attempts and verified-but-unredeemed browser approvals occupy slots.
Cancel, provider failure, successful redemption or expiry frees occupancy; accepted
starts still count for the rolling minute. Exact successful pending-start replays
are free. IPv4 aliases and IPv6 /64 rotation share limits with HTTP accounting.

A database transaction/advisory lock protects count-plus-insert across instances;
counters survive restarts. Network addresses are represented by purpose-keyed
HMAC fingerprints, never raw addresses in the attempt table or logs. A minute
sweep clears expired fingerprints without deleting sessions or assets. See
[operations](operations.md) for limits, rotation and rollout details.

Deploy/migrate all server instances together when the review batch is ready.
The new migration retires existing unfinished handoffs with no trusted origin;
those users must restart login. Saved sessions and inventories/currency remain.
No new plugin package or production configuration is needed. No live restart is
needed to run these tests or continue the security review.

## Security correction 4 — server 0.0.6.6, plugin stays 0.0.6.6

Group history formerly allowed retained dormant/blocked membership rows and could
reveal later moderation reasons. The server now requires active membership and
a non-deleted group before history/cursor access, returning 404 after voluntary
leave, owner removal, block or group deletion. Unblocking alone does not restore
access. Active personal history and active-owner audit retain their existing
scopes. An active rejoin restores personal history access.

Own operation metadata receipts remain available separately by ID or operation
key after departure/deletion. These do not expose history reasons or private
resource payloads. A later moderation action's subject cannot retrieve the
owner's receipt merely by knowing its ID. No new migration/plugin package/Render
setting is needed; previous security migrations still apply when upgrading an
older server. No live restart is needed for these tests or continued review.

## Security correction 5 — server 0.0.6.7, plugin stays 0.0.6.6

Trade list and single-trade reads previously kept the group write lock while
loading every response field, delaying unrelated group mutations. Expiry now
runs and commits in a short locked transaction; response assembly runs afterward
in a fresh read-only PostgreSQL repeatable-read snapshot, without that advisory
lock. Expiry-tracked EF entities are cleared before snapshot queries, and the
snapshot commits before HTTP serialization.

A whole page uses four batched projection lookups (currency, offer lines,
characters/authors and holding definition metadata), rather than queries per
trade/line. Existing pagination, participant scope, sealed writing bodies,
revision checks and command transactions are preserved. Concurrent edits can
complete while the read is being assembled; the response consistently represents
one snapshot and can be an older revision, which later commands must revalidate.
Expiry remains committed if a bad cursor or unknown trade ID rejects the read.

Regression tests reproduce the old held lock for list/single reads, then verify
lock availability and a real concurrent offer edit while projection is paused.
They also check matching revision/quantities, expiry and released reservations
after rejected reads, bounded page query counts, cursor pagination, snapshot
metadata/authors and sealed writing. No migration, plugin update or Render setting
is added. No live restart is needed to continue the security review.

## Security correction 6 — server 0.0.6.8, plugin stays 0.0.6.6

The original TradingBeta SQL broadened all letter-accepting categories, including
owner letter-only policies. Its name-based Correspondence rename was unsafe too:
M3 stored no reliable seed identity. The corrected historical migration preserves
all existing names, allowed types, versions and retired flags. New groups still
receive Books defaults; M3 groups retain their legacy category policy until an
owner explicitly opts in through the category API. The earlier upgrade tests now
check preservation instead of expecting silent default renaming/broadening.

**An already-upgraded database will not rerun TradingBeta or restore original
policy by deploying this binary.** There is no blind rollback of current owner
choices. Use the [review report and repair procedure](operations.md) to review any
potentially affected existing categories against owner intent/pre-M4 evidence.
The current simplified plugin has no general category editor; corrections use
the owner-authenticated API with ETag and idempotency protection. Existing written
items remain intact if the owner narrows future creation permissions.

No new migration ID, plugin package or Render setting is added. No live restart
or production data repair was performed. Future upgrade behavior is fixed;
existing production category policies remain an explicit owner-review task.

## Security correction 7 — server 0.0.6.9, plugin stays 0.0.6.6

All three lower-priority findings are valid. The server now enforces a retained
creation budget (30 groups per original creator, including deleted/transferred
rows), serves readiness from one background probe per process, and rejects duplicate
first/final approvals with new operation keys. Original same-key retries remain safe.

The new migration is `20261015201132_RetainedGroupCreationBudget`. It backfills
creator accounting without deleting data or changing ownership/permissions.
Readiness initially returns unavailable until the background probe succeeds; allow
roughly eight seconds plus scheduling for default outage/recovery detection.
No new Render settings or plugin installation is needed. No live restart is
required to continue the review; apply the migration with the later combined rollout.
The maintainer plans to reset disposable test data later; no wipe was performed.

Local verification for this correction: 113 server/PostgreSQL cases, 67 plugin/helper
cases and one contract/assembly-boundary case passed (181 total), with no skipped
cases. Regression cases reproduced the four approval/resource failures before the
fix. Documentation, startup-wrapper scenarios and model parity are checked too;
the published-server smoke applies the new migration to the disposable local DB.

## Member profile candidate — plugin 0.0.6.7 / server 0.0.6.10

Deploy server 0.0.6.10 and apply `20261015203517_MemberDescriptions` before testing
profile edits. No Render setting change or data wipe is needed; existing members
start with empty descriptions. Plugin 0.0.6.6 remains compatible. Update the plugin
through the same custom repository; InternalName and feed URL stay unchanged.

- Main navigation shows icons beside Home, Inventory, Crafting, Members and Settings.
- Select a member: name, description and the available trade button are the main
  content. Owner-only permissions appear below with three checkboxes and Save
  permissions. The owner's automatic grants are shown briefly, without editable
  self-grants. Currency/items/membership tools remain collapsed and authorized.
- Select your own character: edit multiline text, check the 300-character counter,
  save, switch away/back and verify another member sees it. Empty clears it.
  Another group has its own description. `<3` and ordinary Unicode are allowed.
- Ordinary members cannot see permission controls or edit another description.
  The owner cannot overwrite someone else's text. Validate rejected over-limit
  input; a concurrent edit preserves your draft and offers reload instead of
  silently replacing it. Background refresh must not reset the text being edited.
- Verify permission save still changes potion access and that context/member trade
  buttons and collapsed administration controls remain usable.

Actual Dalamud icon alignment and window layout still require in-game testing;
automated checks cover persistence, access, text limits, conflicts and replay.

Local profile-candidate verification: 115 server/PostgreSQL cases, 67 plugin/helper
cases and one contract-boundary case passed (183 total). The Release plugin builds
with zero warnings/errors and its ZIP includes the existing icon/dependencies
without host libraries. API documentation covers 94 operations and 124 schemas.

## Member controls candidate — plugin 0.0.6.8 / server 0.0.6.11

Deploy server **0.0.6.11** before testing the new combined permission save or recipe
grant, then update the plugin through the unchanged custom repository. No new
migration is added beyond MemberDescriptions; no new Render settings/data wipe.

- Select an active character as owner/currency manager: balance loads automatically,
  displayed vertically as Total / Available / Offered in trades, with Refresh next
  to the list. Amount and Reason labels sit above the fields. Apply an adjustment,
  switch selections and refresh; late results must not replace another selection.
- Gameplay permissions show Trading (checked means allowed) and Craft Potions.
  Turning Trading off needs no reason, cancels open trades/releases offers, and
  leaves pending ownership proposals alone. Saving gameplay preserves recipe grants.
- Manage membership contains Moderator permissions → Potion Creation (define
  recipes). Save grants access to Crafting → Potion recipes. Test creating,
  revising and retiring recipes, then revoke and verify access ends. Craft Potions
  alone must not enable recipe definition, nor does definition grant crafting.
- Saving moderator permission preserves saved gameplay permissions and leaves any
  unsaved gameplay draft alone. Concurrent/stale changes still require reload.
- Membership reason inputs have visible labels. Remove/block/unblock/ownership
  proposal stay disabled until Shift is held; hover explains the gesture. There
  are no membership confirmation checkboxes. Removal/blocking still need a reason.

Actual disabled-hover behavior, modifier handling and spacing require a short
in-game check. Automated verification covers independent grants, revocation,
atomic trading changes, default/legacy behavior, reservations and unchanged
ownership proposals. No role or general moderator-access mechanism is introduced.

Local verification for this candidate: 118 server/PostgreSQL cases, 67 plugin/helper
cases and one contract-boundary case passed (186 total). The matching server has
no pending model changes; the published Linux binary and startup/readiness smoke
use the disposable local database. Actual game UI acceptance remains a live check.

## Group Home candidate — plugin 0.0.6.9 / server 0.0.6.12

Deploy server **0.0.6.12** with migration **20261015232913_GroupAnnouncements**
before testing, then update through the same plugin feed. Existing data/grants are
preserved. No new Render setting or dependency is required. Do not wipe data to
perform this update. The feature is a private candidate requiring live UI review.

- Members → Manage membership: independently grant General Moderation, Manage
  Gameplay Permissions and Create Announcements. Currency/removal authorizations
  are grouped here too. A gameplay manager sees Trading/Craft Potions controls,
  can grant/revoke those, and cannot alter moderator actions. General Moderation
  alone cannot grant crafting. Owner always retains authority.
- Crafting → Info explains access. Without Craft Potions the Potions tab is hidden;
  Books remains available under the weekly quota. Definition-only moderators see
  Potion recipes but not potion crafting. Grant access and allow the minute refresh.
- Home: owner/General Moderation edits description and currency name directly;
  ordinary members see text. Background refresh must preserve an unfinished draft.
  Concurrent saved edits show reload; group name/icon/policy remain owner-controlled.
- Announcements: post six messages and verify only the newest five titles remain.
  Open a title to see the full message; check red Home and launcher-main pings from
  a second character. Only opening each message clears its unread flag. Own posts
  start read. Restart/relog and verify unread state survives. Check an unread group
  can be reached via launcher/group dropdown without interrupting a running trade.
  Main-window closure must not stop notification polling (up to 30 seconds delay).
- Group Settings has Currency, Invitations and Delete Group tabs. Currency retains
  existing PNG/JPG/WebP upload rules. The old combined names section is gone.
- Weekly Policy has Potions and Books tabs. With active/scheduled settings different,
  verify initial inputs equal active values. Schedule one type and confirm the
  other type's scheduled value is preserved; new limits still start at next reset.

Automated coverage checks independent grants/escalation/revocation, current/legacy
migration preservation, concurrent bounded retention, receipt content, individual
read state across restart, no read-audit growth/ETag changes, and former-member/
cross-group access. Actual layout, popup behavior and badges need an in-game check.

Local Home-candidate verification: 122 server/PostgreSQL cases, 67 plugin/helper
cases and one contract-boundary case passed (190 total). Documentation validates
99 operations, 132 schemas and 17 features. The Release plugin builds with zero
warnings/errors; model parity and the published Linux migration/readiness smoke
are checked against the disposable local database.

## Inventory and text layout — plugin 0.0.6.10

Plugin-only update; server stays **0.0.6.12**. No server redeploy, new migration,
new configuration or data wipe is required if that server version is already deployed.
Use the existing Dalamud repository URL and normal plugin update flow.

- Crafting → Books shows only Writing allowance; Potions shows only Potion points.
  Each has two lines: remaining/limit, then reset time.
- Inventory → All, Consumables and Books share search, type/sort filters, item list
  and details/actions. There is no pinned-only checkbox. The separate compact
  consumables window remains available through its command.
- All orders filters Category → Item type → Sorting. Choosing a category resets
  the type selection and limits type choices to that category. Consumables and
  Books have a fixed type family, no category dropdown, and type/sort controls.
  Switching tabs clears an incompatible category/type selection; filters use
  stable type IDs, so Book/Letter/Note cannot select a different type accidentally.
- Writing contents (create, edit and read), group and character descriptions,
  potion descriptions and announcement messages wrap at the field width. Long
  words wrap too. Visual wrapping does not add saved newlines or spend the text
  limit. Explicit Enter/newlines remain part of the text.

Focused in-game check: paste a long paragraph, a long unbroken word, emoji and
several real line breaks. Resize while editing, move the caret across visual line
breaks, replace a selection, Backspace/Delete at a wrap, copy/paste and undo/redo.
Save/reopen and confirm only intentional newlines persist. Repeat in group and
character descriptions. Check the same inventory structure with both empty and
populated Books/Consumables tabs, category-dependent types in All, and potion
use from the details pane. The local 75 helper cases include eight wrapping
cases for original-text preservation, Unicode, edits/caret mapping and resizing;
Release build/package and documentation checks pass. Game rendering remains a
live acceptance check.

## Trade requests and launcher group selection — plugin 0.0.6.11

Plugin-only update; server stays **0.0.6.12**. No new migration, server deployment,
configuration or data wipe is required. Keep the existing Dalamud feed URL.

- The trade window always names the selected group at the top; the final exchange
  review names the group too. While idle/waiting, it has Inbound and Outbound tabs.
- Inbound lists incoming requests from joined groups with sender, group, remaining
  time and Accept. No request opens a window automatically. The empty state explains
  right-click → Amor RP: Trade; the launcher retains its red incoming-request badge.
- Outbound explains the context menu when no target is selected. Context-menu or
  Members → Trade with this member selects Outbound and the character automatically;
  Send request is an explicit action. The old manual recipient dropdown is gone.
- A sent request shows the recipient, waiting status and countdown, plus Cancel
  request. At acceptance, both request tabs disappear and the existing inventory/
  offers/two-stage confirmation layout appears for both participants.
- Closing the window leaves unanswered invitations pending until cancelled/expired.
  Closing an accepted exchange still cancels it. An acceptance that completes after
  local closure is cancelled; a preparation read cannot submit acceptance after close.
- The quick launcher has a group dropdown beside its two icon buttons. It uses the
  same saved selection and pending-action guards as the main header. Incoming requests
  do not pin the current group. Sent requests and accepted exchanges must end before
  switching; attempting a switch opens the trade with its cancel control. Joined-group
  menu entries mark incoming requests and unread announcements.

Focused live check with two characters: open trade empty, send via the context menu,
verify Outbound waiting text/countdown, then recipient red badge → Inbound → Accept.
Both windows should show only the exchange layout and correct group. Complete one
trade and cancel another; request tabs should return afterward. Close/reopen a pending
invitation without losing it, and close during acceptance to check cancellation.
With two shared groups, change launcher selection with the main window closed,
check the main/trade headers agree, and accept an incoming request for the other
joined group. Verify sent requests/accepted exchanges cannot silently switch groups.
The 75 existing helper cases, Release/package and documentation checks pass; actual
in-game tab, timer, dropdown and close/accept interaction remain live acceptance.

## Home overview and group logos — plugin 0.0.6.12 / server 0.0.6.13

Deploy **server 0.0.6.13** with migration **20261016140911_GroupLogos** before
using logo uploads, then update through the same plugin feed. Keep the free-Render
startup migration setting; no paid pre-deploy command, new secret, external image
host or data wipe is required. Earlier plugin clients remain compatible.

- Home has a darker profile card with logo/name/description, then announcements
  on the left and personal resources on the right. Narrow windows stack these
  sections. Long read-only descriptions use Read more / Show less.
- Owners edit group name and description directly in input fields; Save profile
  appears for an edited draft. General Moderation retains description/currency-name
  editing only; group renaming and logos stay owner-only. Drafts survive background
  refresh; concurrent changes require Reload. Currency name remains editable in
  the resources card for authorized members.
- The owner clicks the logo/placeholder to select PNG, JPG or WebP. Selection uploads
  directly; file-dialog cancellation changes nothing. Right-click the logo to remove
  it. Other members see the image/placeholder without edit controls. Images preserve
  their proportions. Sources are at most 2 MiB (operator-configurable lower), 2048px
  per side; stored images are at most 512x512, 1 MiB, static normalized PNG. Animated,
  oversized and malformed images are rejected. Logo replacement never changes the
  currency icon, balances or group identity. Other members see updates on refresh.
- Owner and Create Announcements holders have Title, Message and Publish above
  the announcement list. Members see only clickable titles/dates and unread pings.
  Five-entry retention and full-message popup remain. Successful Publish clears
  the submitted draft; subsequent edits are preserved. No separate composer popup.
- Your resources shows available currency, reserved amount when nonzero, writing
  allowance and (only with Craft Potions access) potion points/reset. Existing
  weekly policy/group settings remain collapsed below the overview for owners.

Live acceptance: upload a rectangular logo and check proportions, switch groups
and characters to check isolation, replace/remove it and verify the currency icon
is unchanged. Cancel the file picker, try a too-large/animated image, and confirm
clear feedback. Edit name/description, refresh while typing, then save/reopen.
Publish a title/message, open it from a second character and verify badges and
five-entry retention. Resize for both side-by-side and stacked layout. Check an
ordinary member cannot edit another group's profile/logo, and General Moderation
still cannot rename the group or change its logo.

Local overview verification: **127 server/PostgreSQL + 77 helper + 1 contract =
205 cases passed**. Documentation validates 102 operations, 133 schemas and 17
features. Release plugin builds without warnings/errors; EF model parity and
the published Linux migration/readiness/Render-ingress smoke pass. Final in-game
layout, image picker and text editing acceptance remain live checks.

### Footer and input limits — plugin 0.0.6.13 / server 0.0.6.14

Redeploy server 0.0.6.14 for the new write limits; no database migration or wipe is
needed. Existing longer text remains readable. If explicitly configured, reduce
ServicePolicy__DefinitionDescriptionTextElements to at most 300 and
ServicePolicy__LetterBodyTextElements to at most 500 (or remove the overrides).

- In the non-owner Home view, the logo has bottom padding inside the profile card.
- Trigger several actions: up to three results appear at the bottom, including in
  the trade window. Each detail button opens that result's own diagnostics.
- Wait 20 seconds to check expiry. Hover a details button, hold it, or keep its
  popup open: the countdown pauses. Close/leave it and check the timer resumes.
  Reopening clears ordinary results. Unresolved action recovery remains available.
- Paste over 300 characters in group/character/potion descriptions and over 500
  into book, letter and note contents. The draft accepts only what fits, including
  selection replacement and Unicode text. Recipe cost reads Potion points per unit.
