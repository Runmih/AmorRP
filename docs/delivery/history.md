# Implementation and acceptance history

This is a condensed historical record, not deployment instructions. Full removed
milestone guides, patch notes and detailed review findings remain in Git history.
Current behavior belongs in the [rules](../product/rules.md); current work belongs
in the [milestone plan](../planning/README.md).

| Checkpoint | Lasting result | Acceptance/evidence |
| --- | --- | --- |
| M0–M1 | Toolchain/bootstrap and identity feasibility | Historical proof; current authentication uses durable XIVAuth pairing |
| M2–M3 | Verified identity, personal currency, inventories, recipes, quotas and media | Maintainer accepted M3 on plugin 0.0.4.2; later migrations preserve stable IDs and assets |
| M4 | Persistent reservations and two-stage atomic trading | Maintainer accepted interaction for progression, 2026-10-15; automated accounting/recovery tests remain required |
| 0.0.6.x | Request recovery, literal chat safety, secure pairing, member profiles, announcements, group events | Security corrections are incorporated in current feature services and regression tests |
| S01 / 0.1.0.0 | Group feature approval and scoped staff grants | Maintainer confirmed major game checks, 2026-10-29 |
| S02 / 0.2.0.0 | Treasury transfers, receipts and disband returns to actual owner | Maintainer confirmed all four game checks, 2026-11-05 |
| S03 / 0.3.0.0 | Trusted offline billing, optional debt, durable collection and disbanded-group claims | Automated tests passed; major game acceptance remains pending |
| 0.3.0.1 | Private home-server origin and durable callback cutover | Maintainer confirmed public service readiness, 2026-11-07; old Render test data was explicitly not restored |
| 0.3.0.2–0.4.0.1 | Attached inventory, writing reader, icon-only panel switch, feature workspaces, Settings | Implemented; native placement and final interaction acceptance remain separate |
| 0.4.0.2 | Seasons replace Communities; explicit global creation permission; no mode switch or old API aliases | Matching source CI passed 370 automated tests; administrator identity remains private deployment configuration |
| 0.4.0.3 | Every verified character belongs to every season; no season join/leave/exclusion; inactive-season lifecycle commands work | [Release evidence](release-0.4.0.3.md); matching source CI passed 372 tests; game acceptance pending |

## Boundaries retained from earlier decisions

- Public Lodestone IDs, name/world hints and first login never grant authority.
  Browser pairing requires the player to possess the browser-only code.
- Same-key retries and receipt recovery never duplicate money/items or repost chat.
- Reservations remain owned by the offerer until atomic trade settlement.
- Collection uses unreserved funds; disbanded-group claims continue to belong to
  the actual former owner, not a deleting moderator.
- Recipes and category policies are not silently broadened during migration.
- Private written bodies are visible only to the holder; moderation grants do not
  bypass group-private content or another player's operation receipts.
- Names/worlds are display fields. Ownership follows stable verified character IDs.
- Applied migrations and immutable packages are retained. Old instructions are not
  a reason to restore retired community flows or a legacy mode.

The old three-owned/six-joined community limits, invitation admission, season
membership exclusions, testing-branch promotion and Render deployment instructions
are superseded. The main feed is the single distribution path. No final 1.0 approval,
capacity claim or production restore drill is recorded by these historical checkpoints.
