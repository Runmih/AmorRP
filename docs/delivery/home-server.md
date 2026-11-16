# Home deployment and Portainer updates

The maintainer's home host runs Docker, Portainer, Nginx Proxy Manager and DuckDNS.
The API is reachable through the configured HTTPS origin; PostgreSQL is private.
Use [deploy/home/compose.yaml](../../deploy/home/compose.yaml), not the local test
Compose file. The home-server cutover was reported working on 2026-11-07.

## Existing installation

Keep the existing database volume, password, vault key and XIVAuth credentials.
From the clean main checkout on the home host:

```sh
cd /opt/amorrp/source
git fetch origin main
git merge --ff-only origin/main
python3 deploy/home/prepare-update.py
```

Fetch and fast-forward before invoking the updater so its code matches the
checkout; do not continue if the checkout has unresolved local changes. The updater verifies latest-main server CI, fast-forwards the clean checkout, builds the
version/commit image and prints its tag. It does not modify the running service.

In Portainer, open **Stacks → amorrp → Editor**. Keep the stack aligned with the
tracked home Compose file and retain private values. Set AMORRP_SERVER_IMAGE to
the exact printed local tag, then **Update the stack** with image re-pull disabled.
Keep the database volumes. The API runs pending migrations before becoming ready.

The administrator variable in Portainer is:

```text
AMORRP_ADMIN_LODESTONE_ID=<your administrator character's numeric Lodestone ID>
```

Its existing API environment mapping is:

```yaml
SeasonPolicy__AdministratorLodestoneIds__0: ${AMORRP_ADMIN_LODESTONE_ID}
```

The stack variable alone is not forwarded without that YAML line. Set the friendly
variable once; do not add a second season setting. There is no season-enable flag.
After a configuration change, update the stack so the running container receives it.

Check the public /health/ready and /api/v2/capabilities endpoints and plugin About.
All verified players receive every season automatically. Only creation-permission
holders see Create season; configured administrators can activate drafts. See
[acceptance](acceptance.md) and [operations](operations.md).

## Fresh installation only

Create private configuration with deploy/home/create-env.py and its required HTTPS
origin, pinned proxy address and administrator ID. It generates a new database
password/vault key without printing them or overwriting an existing environment
file. Supply the private XIVAuth application credentials and register the exact
HTTPS /auth/xivauth/callback URL. Never use this fresh-secret process to update an
existing installation.

Build the server from verified main, prepare the tracked home stack in Portainer,
and retain the generated configuration securely. Host-specific addresses and
credentials must not enter source. No paid service or recovery of the old Render
test database is required by this deployment guide.

## Network boundaries

NPM terminates HTTPS and forwards to amorrp-api:8080 over the existing external
web-proxy network. API/database also share an isolated internal network. Neither
publishes a host port. Pin the proxy container's actual address, configure that
single address as trusted, and keep ForwardLimit=1. Do not trust an entire subnet
or arbitrary client forwarding headers. Keep NPM certificate/data volumes and
unrelated networks intact. Logs are bounded; PostgreSQL storage is persistent.
