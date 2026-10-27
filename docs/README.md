# Documentation index

Current guides describe the implemented project. Latest release:
[0.0.6.21 — authentication isolation and request coordination](delivery/release-0.0.6.21.md).
The plugin remains compatible with server 0.0.6.18, but deploy server 0.0.6.21 to
activate its authentication-lock correction. No API version or database schema change.

Start with [architecture](engineering/architecture.md), the
[source map](engineering/repository-layout.md), [authentication](engineering/authentication.md)
and [development workflow](delivery/development.md). The
[installation feed](delivery/plugin-repository.md) is on `main`.

Product specifications include planned work. OpenAPI labels implemented and planned
operations explicitly. Dated `m0`–`m5` delivery guides and older release records are
historical snapshots; they do not override current source maps or version guidance.

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

Carried feedback and implementation record: [M2 feedback/M3 plan](delivery/m2-feedback-m3-plan.md). M3 is accepted; M4 is the current live-test build.

Current beta release: [0.0.6.18 — crafting, groups and events](delivery/release-0.0.6.18.md).
