# Documentation index

Specification date: 2026-10-06. Target: Amor RP 1.0.

Latest plugin patch: [0.0.6.19 — preserve unfinished group settings](delivery/release-0.0.6.19.md).
It works with server 0.0.6.18 and needs no server redeployment.

M4 implements authentication, communities, currency/icons, potions, Books writing,
quotas, CRUD membership/removal and persistent trading. The first M5 private usability build is
**plugin 0.0.6.15 / server 0.0.6.14**; see [installation and focused testing](delivery/m5-install-test.md).
Install through the [custom testing repository](delivery/plugin-repository.md).
It adds shared literal-message validation and clearer inventory/crafting/member views. The
[M4 installation/test guide](delivery/m4-install-test.md) records upgrade commands,
verification and the live acceptance checklist. Use the
[agent handoff](delivery/agent-handoff.md) in another environment. M3 is accepted
per maintainer testing. The maintainer approved progression from M4 to M5 on
2026-10-15. The [M5 plan](delivery/m5-release-plan.md) defines implementation and
test checkpoints; final 1.0 release gates remain open.

The [M4 interface plan](delivery/m4-interface-plan.md) records the agreed vertical
navigation, horizontal subtabs, contextual controls and quiet updates. OpenAPI
marks implemented versus planned endpoints. Operational recommendations and
unconfirmed choices remain identified in the decision register.

## Reading order

1. [Scope](product/scope.md): what ships and what is deferred.
2. [Rules](product/rules.md): identity, community boundaries, permissions, item behavior.
3. [Interface](product/interface.md): what players and owners see.
4. [Architecture](engineering/architecture.md), [layout](engineering/repository-layout.md),
   and [version compatibility](engineering/versions.md).
5. [API](engineering/api.md), [OpenAPI](engineering/openapi.yaml), and [data model](engineering/data-model.md).
6. [Security](engineering/security.md) and [authentication](engineering/authentication.md).
7. [Roadmap](delivery/roadmap.md), [acceptance](delivery/acceptance.md),
   [development](delivery/development.md), and [operations](delivery/operations.md).
8. [Decisions](decisions.md), [feasibility](feasibility.md), and [expansion](product/expansion.md).

## Document ownership

| Area | Source of truth |
| --- | --- |
| Agreed requirements versus recommendations | [Decision register](decisions.md) |
| Product behavior | [Rules](product/rules.md) |
| HTTP paths, wire schemas, authorization metadata | [OpenAPI](engineering/openapi.yaml) |
| HTTP conventions and event protocol | [API guide](engineering/api.md) |
| Persistence invariants | [Data model](engineering/data-model.md) |
| Toolchain targets, dependency compatibility and update gates | [Version matrix](engineering/versions.md) |
| External claims and integration references | [Sources](sources.md) |

When changing a feature, update its rules, contract, interface behavior, and
acceptance criteria in the same change. Keep decisions short and explain why a
boundary exists. Generated endpoint tables must match OpenAPI.

## Vocabulary

- **Character:** verified FFXIV character; the unit of inventory, permissions, and quotas.
- **Community:** isolated RP environment. Nothing transfers between communities.
- **Owner:** the character responsible for community administration; not a grantable role.
- **Capability:** an individually grantable action permission.
- **Type:** behavior implemented in code, `potion`, `book`, `letter`, or `note`.
- **Category:** community-owned organization label; it never grants a capability.
- **Definition:** owner-authored potion design. **Revision:** immutable version of it.
- **Holding:** quantity or individual item owned by a character in one community.
- **Period:** server-defined weekly interval used to track creation usage.
- **Reservation:** items/currency held for an open trade, still owned by the offerer.
- **Operation ID:** durable identifier for a completed inventory/currency mutation.

Carried feedback and implementation record: [M2 feedback/M3 plan](delivery/m2-feedback-m3-plan.md). M3 is accepted; M4 is the current live-test build.

Current beta release: [0.0.6.18 — crafting, groups and events](delivery/release-0.0.6.18.md).
