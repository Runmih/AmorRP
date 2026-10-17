# M5: implementation and testing plan

The maintainer reported that the 0.0.5.2 trade window feels better and approved
progression to M5 on 2026-10-15. This records acceptance for progression, not a
claim that every production release check or operating drill has been performed.
The baseline is commit `f661bd4` on `m4-trading`; M5 work uses
`main`. The first private usability test build is **0.0.6.0**;
see [installation and testing](m5-install-test.md). Subsequent candidates use
0.0.6.x until release checks and explicit maintainer approval permit 1.0.

M5 completes the existing 1.0 scope. It does not add item types, rich books,
attachments, cross-community transfers, roles, a web administration application or an
official Dalamud submission. Preserve the vertical main navigation, horizontal
subtabs, Inventory default, contextual authorized controls, separate trade window
and explicit user-triggered potion chat. The maintainer removed the earlier
startup permission question in the 0.0.6.1 correction.

## Work and review order

| Checkpoint | Concrete work | Evidence before proceeding |
| --- | --- | --- |
| Usability build | Support literal `<3`; show recipe validation beside fields and an exact message preview; make inventory, crafting and member actions understandable; preserve selection, drafts, scroll and stable form positions | Maintainer can create a recipe, understand rejection, create/use a potion, read a written item and remove a selected holding without troubleshooting instructions |
| Feature-complete candidate | Complete scoped export/deletion request workflows, request status/download, controlled operator processing, maintenance behavior and relevant abuse limits; expose settings/support/privacy information | Real PostgreSQL verifies authorization, ownership resolution, private-body boundaries, idempotency, expiry and maintenance/recovery behavior; operator instructions are executable |
| Installation and recovery candidate | Prepare repeatable distribution/update packages, compatibility checks, version/source/license notices, backup/restore tools and runbooks; review source layout and remove stale documentation | Clean installation, update with existing settings/data, isolated backup restore and restart during a trade using actual release artifacts |
| Release review | Measure stated capacity, confirm supported host/tool versions, review scaling/keyboard/long-text behavior, record support/privacy/persistence decisions and known limits | Focused human tests plus required release evidence in the acceptance checklist; explicit maintainer approval before public 1.0 |

These are review checkpoints within M5, not extra named milestones or a promise to
release everything in one unreviewed package. A usability checkpoint does not
stand in for the remaining lifecycle and operational requirements.

## First implementation slice

The current recipe editor describes limits but has no field-level validation.
The baseline server `ItemText.Chat` and client `ChatCommand.TryCreate` rejected
every angle bracket, including ordinary `<3`. The 0.0.6.0 checkpoint uses shared
Core rules on both sides, permitting unpaired punctuation such as `<3` while
rejecting paired game expressions such as `<t>`, line breaks, control/payload
characters and messages beyond the discovered character/byte limits.
Verify that the exact supported text survives definition, production and use.
Do not simply remove the chat validation or silently alter the author's message.

Inventory, crafting and member workflows need a usability review as well as
styling. Use explicit selected rows/details, clear primary actions, visible
remaining allowances and stable feedback. Preserve the accepted trade interaction;
any regression in confirmation, reservation or recovery behavior blocks delivery.
The Lightless image is an existing visual reference. Its frontend repository is
optional inspiration, not a dependency or permission to copy its code/assets.

## Lifecycle implementation boundaries

Follow the existing [API catalog](../engineering/api.md) and
[OpenAPI](../engineering/openapi.yaml). Mark a route implemented only with its
authorization, failure behavior and meaningful tests. Export is restricted to the
requesting character; held written bodies must not leak through former ownership,
community administration, logs or other players' exports. Exclude provider grants,
tokens, encryption material and other characters' private data.

Deletion must resolve owned communities and open trades before processing. Define and
document the retained audit records and removal of private content before any
operator command can purge data. Destructive production processing is a separate
operator action; testing uses disposable databases. Existing inventories, identity
bindings, grants, quotas, operation keys and protected settings must survive normal
updates. A dormant membership remains a community feature and is not a personal-data
deletion request.

Keep proposed retention and capacity figures identified as recommendations until
the maintainer confirms the policies and measurements. Do not publish placeholder
privacy claims, invent a support contact or silently provision paid services.

## Confirmed starting decisions and later release inputs

The maintainer confirmed both nonsecret preferences on 2026-10-15:

- Distribution initially used private manual test packages. On 2026-10-15 the
  maintainer requested a [custom testing repository](plugin-repository.md) now,
  to simplify updates. The unlisted feed is publicly downloadable; it remains a
  test-candidate channel. Official submission and 1.0 approval remain later gates.
- Support route: [the project's GitHub Issues page](https://github.com/Runmih/AmorRP/issues).
  Show a link and request sanitized reports; do not automatically post reports,
  attach private text or upload diagnostics.

No additional maintainer input blocks the first implementation slice. Before a public release, settle
the operator identity/contact, retention/deletion policy and supported persistence
and backup arrangement. Existing free Render testing can continue; paid services
are not a prerequisite to starting M5 or testing its interface. Never request
credentials in chat. Deployment and live data processing remain explicit operator
steps after reviewable artifacts and instructions exist.

## Validation discipline

Start from the existing 107-test M4 baseline and add tests for actual new behavior
and security boundaries. Keep real PostgreSQL for transactional and upgrade tests;
use helper tests for literal message construction/normalization. No paid
rename/world-transfer test or separate simulated-rename gate is introduced.

Run the relevant checks per slice, then the full release checks for the candidate.
Maintain OpenAPI/catalog/source parity, locked restores, migration/model parity,
host-DLL exclusion and actual ZIP validation. Keep build evidence separate from
live Dalamud rendering and from production hosting/backup evidence. A capacity
measurement in a development environment cannot prove the Render tier's capacity.

Use the [acceptance checklist](acceptance.md),
[operating guide](operations.md) and [source map](../engineering/repository-layout.md)
as the completion criteria. Keep code communityed by actual features; avoid empty
future folders, speculative abstractions and undocumented operator SQL.
