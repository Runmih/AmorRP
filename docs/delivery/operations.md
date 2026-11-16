# Operating the home server

The supported deployment is the maintainer's Ubuntu/Docker/Portainer host behind
Nginx Proxy Manager. [Home deployment](home-server.md) covers the stack and update
mapping. Do not follow retired Render or milestone-specific deployment instructions.

## Routine update

Build verified latest main with deploy/home/prepare-update.py. It refuses dirty or
unexpected checkouts and missing/failed latest-main CI. It builds an immutable
version/commit image and prints the exact tag; it does not redeploy containers.
Apply that image through Portainer with the existing database, vault keys, private
provider configuration and proxy mapping. Keep the current stack definition in sync.
Do not remove volumes or regenerate secrets on an existing installation.

Check /health/ready and /api/v2/capabilities, then perform the focused
[acceptance checks](acceptance.md). Record the actually deployed image/source and
observed versions separately from the latest source/feed.

## Backup and restore

Back up PostgreSQL with a consistent dump and separately retain the vault key ring,
private provider settings, stack configuration and matching source/image provenance.
Keep backup access restricted; test restorations on an isolated target. Verify
identities, balances, holdings, quotas, receipts, private text and restart/replay
behavior before declaring a restore successful. Do not run an experiment on live data.

Application rollback uses a known image only if its schema expectations are still
valid. Do not automatically run migration Down methods or erase data. For a failed
migration, retain logs and current data, identify the cause and recover from tested
backups as an explicit operating action. Startup migration failure blocks readiness.

## Incidents and support

Review bounded container logs, database readiness, resource use and the sanitized
request/operation IDs. Do not paste full environments, tokens, browser login links,
pairing codes, private writing or vault material into chat/issues. A provider timeout
may require re-login but never a new inventory. Unknown mutation outcomes require
receipt reconciliation before another fresh action.

Use the [GitHub Issues page](https://github.com/Runmih/AmorRP/issues) for sanitized
reports. No automatic diagnostic uploads or issue posting is authorized. Track
capacity measurements, retention/export/deletion policy and restore drills as open
readiness work. A CI container build proves neither live host capacity nor a backup.
