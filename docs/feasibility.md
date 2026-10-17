# Sanity check and implementation readiness

Review date: 2026-10-07. **A clean 1.0 remains feasible. M3 is implemented for
acceptance; the complete 1.0 product is still in development.** The remaining
largest engineering risk is atomic trading and recovery, followed by inventory
live inventory/chat behavior and release operations.

## Existing code assessment

Four application projects build against .NET 10 and Dalamud API 15. M0 and M1
passed per maintainer reports; exact hosted/game logs and credentials are not in
this workspace. M2 adds durable IDs/sessions, community/owner/member management,
individual grants, weekly policy scheduling, currency ledger/history/recovery and
plugin screens. M3 implements categories, immutable potion revisions, independent
usage snapshots, letters, inventory and bounded currency media. Trades and
lifecycle/event features remain later milestones. See [M3 setup](delivery/m3-install-test.md).

Real PostgreSQL tests cover caps, invite use, authority revocation, isolation,
balance concurrency and retry behavior. No paid rename/transfer or separate
simulated-rename gate is required. Hosted refresh with the new minimal scope is
an M2 external check. This workspace cannot prove in-game rendering or use your
hosted credentials. Container execution is separately limited by the Microsoft
CDN network policy; CI/Render can supply that build evidence.

## Feasibility by area

| Area | Assessment | Required evidence |
| --- | --- | --- |
| Server + PostgreSQL + Render | Standard, suitable architecture | Deploy/restore a real service and measure target workload |
| Communities/capabilities/quotas | Straightforward domain, moderate transaction work | Concurrent caps and budget tests; product-default review |
| Potion/letter inventory | Small and extensible enough for 1.0 | Immutable revisions, instance ownership and renderer tests |
| Trading | Highest integrity risk; feasible but not trivial | Reservations, stale confirmations, restart and restriction races |
| XIVAuth | M1 accepted; durable adapter built | M2 hosted refresh/revocation behavior |
| Context menu/home world | M1 passed per maintainer | Live test including world travel and missing context fields |
| Chat channels/consent | M1 passed; 0.0.6.1 removes separate permission prompts | Each channel, encoding, error and consent tested in game |
| Maintainable layout | Four projects and focused folders implemented | Human navigation/Windows build and release checks |
| Official distribution | Separate uncertainty | Current platform review; human code understanding/testing/disclosure |

## Important limits

- Character authentication does not prove an honest client or actual in-game chat
  delivery. Server controls assets; the explicit previewed Use action controls legitimate player UX.
- There is no atomic transaction between the database and FFXIV chat. Report failures
  honestly and reconcile request outcomes. Do not promise guaranteed public messages.
- Alt quotas are intentionally independent. Owner moderation, not account tracking,
  manages multiple authorized characters.
- Weekly creation limits alone do not limit stored item accumulation. Capacity
  ceilings, pagination, retention and paid persistence are necessary.
- Owner currency issuance remains powerful. Recorded reasons/history support
  oversight, but owners and authorized minters can intentionally alter RP supply.
- Immutable revisions preserve accepted messages but introduce distinct inventory
  stacks. The UI must explain this rather than silently combine revisions.
- A working custom-distributed release is not automatically an official-repository
  approval; current submission policies require meaningful human involvement.

## What is already sufficiently defined

Product types, per-character budgets, closed community boundaries, action permissions,
UI flows, server authority, endpoint inventory, entity/transaction model, proposed
tree, hosting approach, failure handling, acceptance scenarios and future seams.
Rules that have not been explicitly confirmed are identified as recommendations,
not silently treated as user decisions.

## What is still needed

| Need | Can proceed without it? | Gate |
| --- | --- | --- |
| Review recommended cap scope/defaults/lifecycle decisions | Identity and chat spikes can proceed | Before community/inventory milestone acceptance |
| Live Windows FFXIV/Dalamud test machine and hosted CI | Local .NET builds/tests have passed | Remaining M0/M1 checks |
| XIVAuth developer app, registered callbacks and secret access | Adapter design/fixtures can proceed | M1 |
| Render account/billing/region and test database | Local development can proceed | Deployed M1, paid M3 beta |
| Human maintainer/operator and real two-character testers | Implementation can proceed | M3 feedback and M5 release |
| Confirm data retention/support/distribution and measured capacity | Functional development can proceed | Before persistent beta/1.0 |

No automatic request for credentials is necessary now. When setup becomes relevant,
use secure service configuration rather than pasting secrets into chat or source.
M0 code and local artifacts are built. No Render resources were provisioned and
no XIVAuth application was registered.

## Readiness conclusion

Documentation validation covers 85 API operations and 124 schemas, resolved local
links, endpoint catalog/source parity and all 16 feature IDs. M3 implements 73
operations; 12 remain planned. The [checker](../scripts/check-docs.py) verifies
contract/document consistency, not game behavior. Local verification passed 76 tests,
including PostgreSQL quota/revision/isolation/privacy/media and upgrade scenarios.

Complete M3 live acceptance, including the carried kick and inventory/chat loop,
then start M4 trading. Keep CI/container evidence separate. Finish the full loop
with a small real community before expansion; M5 operational, privacy, measured capacity
and human release checks still block public 1.0.
