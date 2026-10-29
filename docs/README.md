# Documentation index

Current guides describe the implemented project. Latest release:
[0.1.0.0 — community permissions and group staff](delivery/release-0.1.0.0.md).
Update both server and plugin. The server migration preserves existing groups and
owner features. Amor RP API v2 and XIVAuth API v1 remain unchanged.

Start with [architecture](engineering/architecture.md), the
[source map](engineering/repository-layout.md), [authentication](engineering/authentication.md)
and [development workflow](delivery/development.md). The
[installation feed](delivery/plugin-repository.md) is on `main`.

Product specifications include planned work. OpenAPI labels implemented and planned
operations explicitly. Dated `m0`–`m5` delivery guides and older release records are
historical snapshots; they do not override current source maps or version guidance.

## Current expansion planning

The [expansion work plan](planning/README.md) records the 2026-10-29 feature decisions,
dependency order and progress checklist. It links separate plans for economy/groups,
gameplay and integrations. S01 implementation is delivered in 0.1.0.0 with maintainer game acceptance pending;
remaining features are planned.
Start there for new feature work; historical milestone roadmaps remain reference records.

## Reading order

1. [Scope](product/scope.md): what ships and what is deferred.
2. [Rules](product/rules.md): identity, community boundaries, permissions, item behavior.
3. [Interface](product/interface.md): what players and owners see.
4. [Architecture](engineering/architecture.md), [layout](engineering/repository-layout.md),
   and [version compatibility](engineering/versions.md).
5. [API](engineering/api.md), [OpenAPI](engineering/openapi.yaml), and [data model](engineering/data-model.md).
6. [Security](engineering/security.md) and [authentication](engineering/authentication.md).
7. [Expansion work plan](planning/README.md), [historical roadmap](delivery/roadmap.md), [acceptance](delivery/acceptance.md),
   [development](delivery/development.md), and [operations](delivery/operations.md).
8. [Decisions](decisions.md), [feasibility](feasibility.md), and [expansion](product/expansion.md).

## Document ownership

| Area | Source of truth |
| --- | --- |
| Agreed requirements versus recommendations | [Decision register](decisions.md); current expansion [decisions](planning/decisions.md) |
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
- **Group:** an owned subgroup inside a community, with its own membership and private announcements.
- **Owner:** the character responsible for a community or group; ownership is not a grantable capability.
- **Capability:** an individually grantable action permission.
- **Type:** behavior implemented in code, Alchemy and Cooking consumable types, plus `book`, `letter`, and `note`.
- **Category:** community-owned organization label; it never grants a capability.
- **Definition:** an authorized recipe author’s consumable design. **Revision:** immutable version of it.
- **Holding:** quantity or individual item owned by a character in one community.
- **Period:** server-defined weekly interval used to track creation usage.
- **Reservation:** items/currency held for an open trade, still owned by the offerer.
- **Operation ID:** durable identifier for a completed inventory/currency mutation.

Historical feedback and implementation record: [M2 feedback/M3 plan](delivery/m2-feedback-m3-plan.md).

Feature baseline before maintenance patches: [0.0.6.18 — crafting, groups and events](delivery/release-0.0.6.18.md). The latest maintenance release is linked above.
