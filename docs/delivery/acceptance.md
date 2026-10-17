# Acceptance and release checks

These are required future checks, not existing automated test results. Use pure
unit tests for domain rules and real disposable PostgreSQL for transactional/race
tests. In-game behavior requires human tests in FFXIV.

## Feature traceability

| Features | Required evidence |
| --- | --- |
| F01 login | Verified ID/key resolves the existing character; wrong ownership, callback replay, expiry, refresh/logout; paid rename/transfer checks waived and no separate simulated-rename gate (A16) |
| F02 communities | 3/6 caps under concurrency; invites expire/revoke; leave/remove/restore; accepted transfer; deletion |
| F03 switching | Correct community/character always shown; dropdown outside tabs; pending trade reconciled before switching; refresh preserves drafts and displays stale/error state |
| F04 permissions | Separate General Moderation, Manage Gameplay Permissions and announcement grants; no delegated moderator escalation; revoke in open dialog; owner transfer updates authority |
| F05 currency | Issue/remove history; insufficient/reserved/overflow denied; exchanges conserve supply; authorized PNG/JPEG/WebP icon upload <=128x128, isolation/replace/remove/restart |
| F06 categories | Seed/add/rename/retire; old item remains readable; category never grants creation privilege |
| F07 definitions | Owner/authorized recipe-editor revisions; crafting permission alone cannot define; revocation blocks writes/replay; cost changes invalidate old preview; retirement preserves copies |
| F08 production | Correct quantity*cost; independent alt quotas; grant/rejoin does not refill; no concurrent overspend |
| F09 letters | Five/week default; limit changes scheduled; editable/locked author rules; bounded body |
| F10 inventory | Both views; search/pagination; details privacy; accurate available/reserved quantities |
| F11 use/chat | No chat permission window on startup or reload; explicit Use posts the exact preview to a valid chosen channel; correct one-item commit; duplicate and chat-failure UX |
| F12 trades | Safe potion/letter previews, invite/accept, two-stage approval/cancel-on-edit, leading-zero normalization, stale revision rejection, reservations, completion/cancel/expiry races |
| F13 history | Personal visibility/owner scope; reasons and actor; no letter body or secret leakage |
| F14 recovery | Request ambiguity, reconnect gaps, restart during trade, durable idempotency |
| F15 settings | Character/backend scoping; credential clear on endpoint change; upgraded config; logout cleanup |
| F16 lifecycle | Compatible versions, maintenance, export/deletion, backup restoration, install/update/support |
| F17 Home/announcements | Latest five under concurrent posts; trimmed IDs unavailable; active-member visibility; exact per-character read markers survive restart; no audit growth/version churn on read; both badges clear only for opened messages; current policy inputs preserve the other scheduled type. |

## Essential invariant scenarios

Normal durable authentication tests must verify that a provider-verified ID/key
resolves the existing internal Character record and authorizes that character's
data. Client-supplied IDs alone and another ownership binding must not authorize
it. Cover returning login/session renewal and explicit binding recovery within
the authentication suite. Name/world is never an ownership lookup or fallback.
Live paid rename/transfer checks are waived by A16 and recorded as skipped for
cost; no separate simulated-rename/transfer gate is required. Other live
login/provider checks remain required.

1. Substitute a community B holding/definition/member/currency/trade/category ID into
   every applicable community A endpoint; deny without mutation or private-data leak.
2. Send 20 parallel production requests with only enough budget for five copies;
   exactly allowed spending succeeds, no negative quota, no partial output.
3. Repeat a command with the same key/payload; original operation/result returned.
   Change payload with same key; reject. Try an expired key after replay purge;
   reject rather than treating it as new. Recover by operation ID.
4. Concurrent consume, discard, administrative removal and offer of last potion;
   at most one valid debit/reservation succeeds.
5. Two users confirm revision N while a third request edits the offer; no exchange
   using stale approval. Editing after either acceptance cancels the whole trade;
   both must explicitly approve the final popup before any transfer.
6. Race completion against owner restriction/removal and cancellation/expiry;
   exactly one serial outcome, no stranded reservations or partial transfer.
7. Restart server between confirmation requests and after commit before response;
   reconcile persisted state and do not duplicate exchanges or chat.
8. Leave/rejoin and revoke/regrant after spending allowance; same week's usage
   remains. Two authorized alt characters deliberately get independent allowances.
9. Create at exact weekly boundary and concurrent policy edit; server period and
   scheduled revision decide consistently. Test daylight-saving display separately.
10. Trade letter away and back; author still cannot edit it. Former holders cannot
    read current body through API or cache after access removal.
11. Recipient inventory full; entire trade fails without either side losing assets.
12. Authorized currency delta overflows or spends reserved units; reject atomically.

## In-game and usability checks

Use at least two real characters. Test home-world extraction during travel, same
name on different worlds, undefined context data, party/no-party, each Linkshell
slot, Emote formatting, byte/Unicode/percent/control text, and rapid repeated clicks.
No silent destination changes. Simulate FFXIV chat failure after confirmed consumption.

Measure plugin frame impact during large inventory search and server timeout.
Asynchronous requests never block drawing; cancel/dispose unsubscribes all events.
Verify settings at UI scales, keyboard navigation, absence of legacy permission prompts, restrictive
permissions, offline mode, and readable failure messages with a request ID.

## Capacity and operating checks

Provisional R14 target: 50 connected clients across five communities, ten simultaneous
trades, inventories up to 10,000 holdings/community. Initial acceptance target: p95
ordinary API responses under 500 ms in same-region synthetic load (excluding
external authentication); no unbounded memory growth or invariant violation during
a one-hour mixed workload. Real player latency is separately observed; synthetic
measurements do not guarantee global latency. Adjust tier/target with measured evidence.

Exercise backup export and restore into isolated database; verify ledger/holdings,
quota snapshots, reservations and migrations. Record achieved RPO/RTO. Restart
service while trades are open. Alerts/reporting and budget settings must be exercised.

## 1.0 release gate

- All features above pass; no critical duplication, cross-community, auth or data-loss bug.
- XIVAuth and current Dalamud/channel/context integrations demonstrated end to end.
- Fresh clone builds with documented toolchain; clean install and update by new tester.
- [Version matrix](../engineering/versions.md) gates passed: exact tool/package/image
  pins and locked CI restore, host-provided DLLs excluded from plugin ZIP, matching
  `AmorRP` internal name/manifest/API level, actual game/host/OS test versions recorded.
- Current and previous advertised compatible client releases tested against server;
  incompatible protocol/type/settings versions fail clearly; migrations and restore
  tested on the selected PostgreSQL major and actual release artifacts.
- Target file structure implemented; no template text/assets, ignored required
  manifests, scattered authoritative state, or secrets in repository/package.
- OpenAPI matches server routes and contract tests; old supported client behavior tested.
- Paid persistence/backups, restore drill, operator/support contact, privacy/retention
  notice and source-license links ready; testers agree to persistence expectations.
- Distribution path selected. Official submission requires actual human review,
  personal testing, AI-use disclosure and human-written submission text under current policy.
- Human maintainer signs off scope/usability, deployment and maintenance responsibility.

## Recorded milestone status

M4 progression: on 2026-10-15 the maintainer reported that the 0.0.5.2 trade
interaction feels better and requested M5. Proceed using the
[M5 plan](m5-release-plan.md). This is not evidence of a completed production
restore/capacity drill or final 1.0 usability approval.

M0: testing reported successful 2026-10-06. M1: passed per maintainer report
2026-10-07. M2: implemented for testing; local verification and the new live exit
checklist are documented in the [M2 guide](m2-install-test.md). Earlier acceptance
does not claim a new refresh scope was demonstrated. Existing 1.0 feature rows
remain release criteria, not all completed features.

## M2 feedback carried forward

M2's reported in-game removal failure is open, not accepted. Deliver its fix in
M3 before completing inventory acceptance; no M2.1 build is planned. Require a
real plugin reasoned kick, target access loss and one committed history event.
Verify blank-reason validation and persistent rejection details across refresh,
active-only default roster with owner inactive recovery, shared dropdown, global
and 60-second read refresh preserving drafts, and success-only join/create resets.
Currency icons are an explicit 1.0 requirement, including valid format/dimension
and malformed/animated/oversize rejection, authorization, persistence and texture
lifecycle. See the [implementation plan](m2-feedback-m3-plan.md).

M3 build evidence: 76 tests passed on 2026-10-07, including meaningful inventory
accounting, private-letter, image, removal and M2-upgrade checks. M3 is built for
live acceptance; this does not complete the in-game gates or M4/M5 release criteria.
See the [M3 test guide](m3-install-test.md).

Live feedback on 2026-10-14 reports successful item removal, member removal,
unblocking and rejoining. The item-removal interaction and overall interface are
not accepted as final. Potion production for owner/authorized member remains an
open functional check; confirm the live failure's cause before prescribing a fix.
The agreed future behavior separates removal from blocking. See the
[feedback record](m3-feedback-review.md); this does not mark all M3 checks passed.

M3.1 follow-up: the maintainer confirms effective potion-creation permission and
letter creation for both members. Potion production remains unresolved; determine
the disabled-button/request/outcome state before claiming its failure cause.

Final M3 result supersedes the open reports above: on plugin 0.0.4.2 the maintainer
confirms both participating characters create/use potions with game-chat posting
and accepts progression to M4. This is milestone acceptance, not final UI or 1.0
release acceptance. Apply the [M4 interface requirements](m4-interface-plan.md),
including stable background reads and vertical navigation/horizontal subtabs.

## M4 live acceptance still required

Server and plugin automation does not validate Dalamud rendering or a live context
menu. Run the focused [M4 two-character checklist](m4-install-test.md) for navigation,
quiet updates, trade invitations/review/confirmation, rejoining/blocking and restart
recovery. Do not repeat the accepted M3 checks except a short upgrade regression.
M4 implementation is not a claim of final 1.0 acceptance; M5 operational gates remain.

## M5 first usability checkpoint — 0.0.6.0

The implementation and focused live steps are in the [M5 guide](m5-install-test.md).
Literal `<3`, field feedback and the inventory/crafting/member organization are
implemented for private testing. Automated checks do not establish live UI/chat
acceptance. Lifecycle, restore/capacity, distribution/update and final approval
gates remain open within M5; this checkpoint is not 1.0.

Home overview/logo addition to F17: owner input fields and clickable logo picker,
read-only ordinary profiles, inline authorized Title/Message/Publish, responsive
announcements/resources cards. Verify secure media replacement/cleanup and
legacy migration preservation plus the focused in-game checklist.
