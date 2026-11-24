# Amor RP documentation

The current product is Seasons with RP groups inside them. Every verified player
belongs to every season automatically. Season creation requires `seasons.create`;
configured Lodestone administrators receive it by default. There is no mode switch.

Start with the [current milestone plan](planning/README.md), [product rules](product/rules.md)
and [source map](engineering/repository-layout.md). Source release versions live
in [Directory.Build.props](../Directory.Build.props); the published version and exact
Windows package are recorded in [repo.json](../repo.json) and packages/*.json.

| Need | Guide |
| --- | --- |
| What works, what remains, next milestone | [Milestone plan](planning/README.md) |
| Product boundaries and feature status | [Scope](product/scope.md), [rules](product/rules.md), [interface](product/interface.md) |
| Agreed requirements and future feature detail | [Decisions](decisions.md), [expansion decisions](planning/decisions.md) |
| Where code belongs | [Architecture](engineering/architecture.md), [source map](engineering/repository-layout.md) |
| HTTP routes and wire shapes | [API catalog](engineering/api.md), [OpenAPI](engineering/openapi.yaml) |
| Persistent identities, financial invariants | [Data model](engineering/data-model.md) |
| Authentication and access checks | [Authentication](engineering/authentication.md), [security](engineering/security.md) |
| Build, test and publish | [Development](delivery/development.md), [versions](engineering/versions.md), [installer feed](delivery/plugin-repository.md) |
| Maintain the home server | [Deployment](delivery/home-server.md), [operations](delivery/operations.md), [Discord bot setup](delivery/discord-bot.md) |
| In-game and operating acceptance | [Acceptance](delivery/acceptance.md) |
| Continue development | [Agent handoff](delivery/agent-handoff.md) |
| Important past changes and verified acceptance | [History](delivery/history.md) |
| External technical references | [Sources](sources.md) |

Current guides describe implemented behavior. Planning documents identify future
work explicitly. Automated CI does not establish in-game acceptance or a production
restore drill. Keep those results separate.

Vocabulary: a **season** isolates its currency, items, quotas and RP groups; a
**character** is the verified owner of personal assets; a **group** has independent
membership and staff permissions inside a season. **Capabilities** grant individual
actions, **definitions** are versioned recipes, **holdings** are owned items, and
**reservations** hold assets for an open trade. Global season administration is
separate from season ownership and group staff authority.
