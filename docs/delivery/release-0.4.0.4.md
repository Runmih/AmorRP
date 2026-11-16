# Amor RP 0.4.0.4 — Maintenance consolidation

This release consolidates the current seasonal product without adding new gameplay,
changing authority or migrating the database.

## Code and release tooling

- Removed the obsolete request-shape adapter. Current commands, receipt checks,
  scope checks and retry resolution now use the original current request directly.
- Renamed internal command kinds/refresh handlers to Seasons and aligned media
  filenames with their existing SeasonLogo classes. Saved JSON/configuration keys,
  protected-data purposes, window IDs and database mappings remain unchanged.
- Shared MediaAssets handles image fingerprints, normalized asset construction,
  season/purpose-scoped reads and prior-asset cleanup for currency, season and group
  images. Feature services retain preflight and locked authorization, ETags,
  receipt ordering and transaction ownership. Missing/replaced images return 404.
- Directory.Build.props defines the release version once for plugin/server/Core/
  Contracts. Validators, fresh-image defaults and the home updater read it.
  Capabilities reports the actual built server assembly version.
- ApiProtocol defines the unchanged API v2/minimum version floor. ServiceCompatibility
  checks it centrally, with tests for supported maintenance releases, malformed
  versions, unsupported APIs and newer minimum-client requirements.
- Removed retired milestone packagers and testing-branch/candidate publication paths.
  Main is the single source/feed path; immutable versioned packages and exact-source
  CI gates remain. All main pushes run checks; built-in-token publication dispatches
  verification explicitly so the home updater is not stranded on an unchecked feed
  commit. Old published packages and applied migrations are preserved.

## Documentation and milestone plan

Current rules, interface, data model, security, versions, handoff and home operations
are concise current guides. Superseded milestone install guides and individual old
patch narratives are condensed into [history](history.md); full originals remain
in Git history. The [single milestone plan](../planning/README.md) records S01/S02
acceptance, S03/season/window game checks still outstanding, operating readiness and
S04–S11 dependencies. Detailed accepted future requirements are retained.

No game or production operating checks are claimed by this pass. Taxes, shop,
permits, gathering, classes, integrations and export/deletion remain planned.

## Deployment

Plugin/server source is 0.4.0.4. The unchanged seasonal protocol floor is 0.4.0.3;
a plugin update does not require a home-server update solely to keep connecting.
Apply server 0.4.0.4 through the normal verified build/Portainer process when desired
to receive its maintenance refactor. No new migration, administrator variable,
provider configuration or key rotation is needed. Retain the existing private
configuration, database and volumes.

Publication uses the exact Windows artifact after matching source CI succeeds.
Verify the public feed and downloaded ZIP digest, then keep game acceptance separate
from automated build/test evidence.
