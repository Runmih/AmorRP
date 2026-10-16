# Roadmap to 1.0

Milestones are ordered by dependency and evidence. Dates require team availability,
integration results and tester feedback; none are promised by this specification.
Do not mark a milestone complete because its code exists without its exit evidence.

| Milestone | Deliverable | Exit evidence |
| --- | --- | --- |
| M0: build and boundaries | Replace old scaffold with clean four-project layout, verify version matrix and lock dependencies, clean ignores, local PostgreSQL and CI | Fresh checkout builds server/core/contracts; plugin builds/packages/loads on supported platform; toolchain, host references and exact commands recorded |
| M1: feasibility spikes | Real XIVAuth character login on Render; live context-menu and consented chat spike | Valid login, wrong identity rejection, all selected channels tested, reconnect shown; no account-ID collection |
| M2: group foundation | Groups, invites, membership/owner lifecycle, capabilities, policies, currency, isolation | Two characters in two groups; caps and all cross-group denials tested with real PostgreSQL |
| M3: playable inventory alpha | Carried M2 removal/UI fixes, currency icons, potion definitions/copies/points, letters/quota/editing, extended and compact inventory, consented use | Plugin kick/access loss/history demonstrated; shared navigation/refresh/form resets and bounded icons verified; owner plus two members complete creation/read/use session; restart preserves data; duplicate requests verified |
| M4: interface and trading beta | Vertical main navigation/horizontal subtabs, integrated authorized actions, stable background updates, CRUD consolidation and separate blocking; then context-menu trades, reservations, revisions/confirmation and readable history | No jarring reloads or lost selection/drafts; membership actions behave distinctly; concurrent accounting/recovery checks pass; two real players trade successfully |
| M5: 1.0 candidate | Installation/updates, privacy/support, export/deletion, backup/restore, capacity, UI polish | Entire release checklist passes; no unresolved critical defect or integration gate; human release review |

M0 in-game testing was reported successful on 2026-10-06. The maintainer reported
**M1 passed on 2026-10-07**. Paid rename/world-transfer checks remain waived;
there is no separate simulated-rename test gate. Exact external test logs and
credentials were not supplied to this workspace and are not inferred.

M2 is implemented for testing; see the [upgrade and exit checklist](m2-install-test.md).
Durable login is included as a prerequisite: the memory-only M1 credentials cannot
authorize product APIs. M2 adds PostgreSQL-backed sessions and `character refresh`
provider scope. Hosted renewal and the new plugin workflows require M2 acceptance.
Hosted CI/container execution needs separately recorded evidence.

## M2 feedback carried into M3

The maintainer reported one functional bug on 2026-10-07: kicking a member fails
without visible feedback/history. M2 acceptance remains open for that path.
Proceed with one M3 build containing its fix; no separate M2.1 build is scheduled.
Removal and persistent diagnostics come first, before inventory work relies on
membership authority. See [feedback and implementation order](m2-feedback-m3-plan.md).

M3 also includes a shared dropdown above tabs, global/manual and 60-second visible
window refresh, active-only default roster with owner recovery view, success-only
join/creation form resets, and owner-uploaded PNG/JPEG/WebP currency icons up to
128x128. Icon upload is backend functionality and an explicit expansion of 1.0
scope. Cosmetic polish stays M5; usability improvements start now.

M3 initially shipped as plugin 0.0.4.0/server 0.0.4. Plugin 0.0.4.1 improved
permission feedback and 0.0.4.2 fixed the potion button ID collision. The maintainer
now accepts M3 for progression after both participating characters create/use
potions with chat posting, plus the earlier letters and membership checks.
See the [M3.1 guide](m31-install-test.md). No M2.1 release was created.

## M3 review and M4 direction

M3 is accepted functionally for progression; the current layout is not accepted as
the final interface. The latest direction on 2026-10-15 requires vertical main
navigation and horizontal subtabs, Inventory default, profile Home, dedicated
creation and contextual authorized controls. History belongs in Settings and
there is no Owner tab. Stable background updates must replace jarring full-view
reloads. See the [M4 implementation plan](m4-interface-plan.md), then the
[historical feedback](m3-feedback-review.md). CRUD consolidation and separate
blocking precede trading; final styling/refinement remains M5.

Follow-up: the owner's missing recipe was caused by rejected literal `<3` text,
not an owner authorization denial. Supporting `<3` and clear recipe-message
validation feedback are explicitly deferred to M5. The permission interaction
and potion creation/use are now confirmed working after the M3.1 patches.

## Dependency priorities

- M1 precedes large UI investment: identity, home-world context extraction and
  channel posting are externally constrained integration risks.
- Existing project code can be replaced completely. M0 does not need a legacy
  sample-profile migration. Follow the [version matrix](../engineering/versions.md)
  and validate supported combinations again before M5 release.
- Accounting and isolation precede trading; reservations reuse proven holdings
  and currency transactions rather than creating a second accounting path.
- Owner administration is part of M2/M3, not a manual database-edit workaround.
- Letters and potions finish before adding more item types.
- Beta data has a stated persistence policy and paid database before testers rely
  on it. Test and production databases are separate.
- M2 uses per-verified-character group caps, including independent alts; confirm this during M2 acceptance. Resolve quota/week/content/lifecycle rules before
  M3; distribution/capacity/privacy policy before M5.

## Product feedback gates

At M3: can unfamiliar players find their group, remaining allowance and potion
message without help? Does five letters/week feel appropriate? Does compact view
stay useful during RP? Ask owner whether currency history is sufficient oversight.

At M4: can each player understand the final offer, both approval stages and why an edit cancels the deal?
Does restriction behavior match owner expectations? Can the group recover from
disconnect without a maintainer editing data? Record observed defects and update scope.

At M5: a clean install by a new tester, an upgrade with old settings, and a server
restart during a trade must work. Human maintainer can navigate the tree, explain
transactions/authentication, restore data and publish the package.

No public 1.0 before [acceptance](acceptance.md) and external dependencies in the
[feasibility review](../feasibility.md) are resolved. Scope may be reduced through
an explicit product decision; safety/integrity cannot be replaced by UI-only checks.

## M4 implementation status

M4 is implemented on `m4-trading` with plugin/server 0.0.5.2. See the
[upgrade and focused two-character test](m4-install-test.md). Database checks cover
conservation, reservations, stale confirmation, concurrent completion/removal,
restart/expiry, permission/isolation, overflow/net capacity, replay and M3 upgrades.
The maintainer reported on 2026-10-15 that the revised trading feels better and
approved progression to M5. This does not assert that every production release
check has passed. M5 remains the 1.0 candidate; no additional M3.2 or cosmetic-only
gate is introduced. Its implementation and review checkpoints are in the
[M5 release plan](m5-release-plan.md).

M4 trading follow-up **0.0.5.2** adds a separate inventory/offer window, arrows,
currency focus-loss saving and leading-zero normalization. It replaces manual
publish/reload with two-stage approval. Any edit after either acceptance cancels
the deal. Server upgrade plus DoubleTradeConfirmation migration are required;
this remains M4 live acceptance, not a separate milestone.

M5 first private usability checkpoint: **0.0.6.0** implements literal `<3` support,
shared validation/field feedback, clearer inventory/crafting/member panels and the
GitHub Issues support link. Use the [focused test](m5-install-test.md). Versions
remain 0.0.6.x while testing; 1.0 requires release evidence and explicit approval.
Lifecycle/operating work is the next M5 checkpoint, not another milestone.

Before proceeding to further M5 work, plugin **0.0.6.1** addresses the maintainer's
login/startup screenshots: remove chat permission and M1 tooling, show a single
login action, and fix/hide the live server endpoint. This is a plugin-only patch
for existing server 0.0.6.0, not a new milestone or server migration.
