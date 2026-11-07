# Home-server cutover with Portainer and Nginx Proxy Manager

Prepared 2026-11-05 for the maintainer's fresh installation. The maintainer reports
that the Render test database is unavailable and explicitly chose a fresh database;
no paid recovery or data migration is required. Existing communities, characters,
assets and sessions are not being restored. This guide does not claim that the new
service has already been deployed or that public login has passed.

The existing public HTTPS hostname will replace the disposable website. The
maintainer chose direct hosting, accepting public visibility of the home connection's
public IP. Do not commit LAN addresses, machine identifiers, filled configuration,
provider secrets, database passwords or vault keys. Use the hostname, never a changing
public IP, as the plugin origin. The Dalamud installation repository URL stays the same.

## Deployment shape

Nginx Proxy Manager (NPM) terminates HTTPS and forwards to `amorrp-api:8080` over
its existing external `web-proxy` network. The API also joins a separate internal
network with PostgreSQL 18. Neither application service publishes a host port.
The API uses the exact pinned NPM container address as its single trusted proxy;
Render header handling is explicitly disabled. PostgreSQL has a persistent named
volume, containers restart automatically, and logs rotate with a bounded size.

Use [the home stack](../../deploy/home/compose.yaml), not the development
[Compose file](../../deploy/compose.yaml), which intentionally contains local test
credentials and host port mappings. The existing Dockerfile builds the current
0.3.0.0 backend. No backend behavior, API version, provider endpoint, scope or
migration definition needs to change for this host move.

## 1. Pin the existing proxy address

In Portainer, edit the existing NPM stack. On its proxy service, retain all current
network connections and turn the `web-proxy` entry into a mapping with
`ipv4_address` set to the container's currently observed address. For example, if
it already has `default` and `web-proxy`, use this shape (substitute the real pinned
address before deploying):

```yaml
services:
  proxy:
    # Keep existing image, ports, volumes, environment and other settings.
    networks:
      default: {}
      web-proxy:
        ipv4_address: REPLACE_WITH_CURRENT_NPM_CONTAINER_IP

networks:
  # Keep the existing default network if the stack uses it.
  default: {}
  web-proxy:
    external: true
```

If only `web-proxy` is connected now, keep only that network. Do not disconnect an
existing database or remove NPM's certificate/data volumes. Updating the proxy
briefly restarts ingress. Confirm the old test website still loads afterwards.
Recheck the proxy address after recreation with:

```sh
sudo docker network inspect web-proxy --format '{{range .Containers}}{{println .Name .IPv4Address}}{{end}}'
```

Do not trust the entire Docker subnet or enable trust-all forwarding. NPM 2.16.0
[appends its observed client to X-Forwarded-For](https://github.com/NginxProxyManager/nginx-proxy-manager/blob/v2.16.0/docker/rootfs/etc/nginx/conf.d/include/proxy.conf).
The backend's existing trusted-hop handling consumes that rightmost value with
ForwardLimit=1, ignoring a client-supplied spoofed prefix. Changing NPM's address
without updating the API trust setting would collapse client accounting to a proxy
address, so address pinning is part of the deployment.

## 2. Build the server image on Ubuntu

Use the existing SSH login from the LAN. These commands create a private application
working directory owned by the current operator; no host username is hardcoded.

```sh
sudo apt-get update
sudo apt-get install -y git python3
sudo install -d -m 700 -o "$(id -u)" -g "$(id -g)" /opt/amorrp
git clone --depth 1 --branch main https://github.com/Runmih/AmorRP.git /opt/amorrp/source
cd /opt/amorrp/source
sudo docker build --file deploy/Dockerfile --tag amorrp-server:0.3.0.0 .
```

Run this once for initial deployment. It builds on Ubuntu using the pinned SDK and
runtime, not on Windows and not through a Portainer editor that may lack build
support. The Portainer stack uses that locally built image and `pull_policy: never`.
There is no new registry account, registry password or public image to manage.

Record `git rev-parse HEAD` and the image ID for the deployment:

```sh
sudo docker image inspect amorrp-server:0.3.0.0 --format '{{.Id}}'
```

## 3. Create private settings and configure XIVAuth

Use [create-env.py](../../deploy/home/create-env.py) on Ubuntu, supplying the public
HTTPS origin with no trailing slash and the exact pinned NPM address:

```sh
python3 deploy/home/create-env.py \
  --origin https://YOUR_PUBLIC_HOSTNAME \
  --proxy-ip YOUR_PINNED_NPM_IP \
  --output /opt/amorrp/amorrp.env
```

It creates a 0600 file containing a random database password and 32-byte vault key,
without printing either. It refuses to overwrite an existing file. Reuse the same
file and key on every update; changing the vault key can make saved grants and
receipts unreadable, and changing the configured password does not change an already
initialized PostgreSQL volume's password.

Copy the file privately to your Windows machine using the existing SSH key/agent,
then load it into Portainer's stack environment variables. For example in PowerShell,
substitute your existing SSH target (do not send the filled file in chat):

```powershell
scp YOUR_SSH_TARGET:/opt/amorrp/amorrp.env .\amorrp.env
```

In the existing XIVAuth application, register this exact callback using the chosen
hostname:

```text
https://YOUR_PUBLIC_HOSTNAME/auth/xivauth/callback
```

Reuse that application's client ID and secret. Do not change its provider API URLs,
OAuth scopes, PKCE/pairing flow or application visibility setting. Enter its credentials
locally as `XIVAUTH_CLIENT_ID` and `XIVAUTH_CLIENT_SECRET` in Portainer after loading
the generated file. The empty values deliberately prevent deployment until configured.
The callback's hostname must match the generated AMORRP_PUBLIC_ORIGIN exactly.
Keep a private copy of these two final values with the original configuration.

## 4. Deploy the Portainer stack

Create a standalone Docker stack named **amorrp**. Paste the contents of
[deploy/home/compose.yaml](../../deploy/home/compose.yaml) into its web editor, load
the generated environment file and fill in the two XIVAuth values above. Do not
select image re-pull for the local API image. Deploy the stack.

Wait for PostgreSQL to become healthy. The API applies all current migrations before
serving requests and stops on migration failure. Its startup log should show database
migrations applied, followed by listening on port 8080. Configure only one API instance
with this automatic migration mode. Do not paste full environment screens or OAuth
callback queries into support messages.

## 5. Route the existing public hostname to the API

Edit the existing NPM proxy host for the selected public hostname:

| NPM field | Value |
| --- | --- |
| Scheme | http |
| Forward hostname | amorrp-api |
| Forward port | 8080 |
| Access | Publicly Accessible; login/callback must not have NPM Basic Auth |
| Certificate | Existing valid certificate for this hostname |
| Force SSL | Enabled |
| HTTP/2 | May remain enabled |
| WebSocket support | Not required for current polling transport |

Forward the whole hostname, including `/auth/xivauth/callback` and `/api/v2/`; do
not strip a prefix or rewrite the callback under an API subdirectory. Preserve NPM's
normal Host and forwarded headers. No additional router ports, SSH exposure, CORS
policy or TLS certificate in the API container are required.

The disposable website can remain stopped or available only through its separate
local hostname. Do not delete the NPM/DuckDNS stacks, certificate volumes or unrelated
applications. The same public hostname now belongs to the API rather than the test page.

## 6. Verify before publishing the plugin endpoint patch

From outside the LAN (for example mobile data), open:

```text
https://YOUR_PUBLIC_HOSTNAME/health/ready
https://YOUR_PUBLIC_HOSTNAME/api/v2/capabilities
```

Readiness must return `status: ok`; capabilities must report API 2 and server 0.3.0.0.
The root `/` may return 404 because this is an API; that does not indicate a failure.
Readiness is cached and can take several seconds to become healthy after startup.

The plugin-only 0.3.0.1 candidate prepares the new HTTPS origin in ProductServer.
Build/Windows verification can happen beforehand, but publish that candidate to the
main feed only once these public routes work and the callback is registered. The backend
stays 0.3.0.0; the plugin still requires server 0.3.0.0 and API 2. No API version change
or fictitious coordinated backend feature release is needed.

Fresh sign-in is required. Saved Render sessions and pending commands remain bound to
the old origin; they must not be retargeted to the empty database or replayed as fresh
mutations. No old data recovery is attempted. Test browser approval/pairing, access to
one fresh community, a two-character trade, and reconnect after restarting the API.
The bot and further economy features remain separate work.

## Backups and updates

Create a private database backup on Ubuntu after the first successful setup and before
updates, using [backup.sh](../../deploy/home/backup.sh):

```sh
sudo sh /opt/amorrp/source/deploy/home/backup.sh /opt/amorrp/backups amorrp
```

It creates a custom-format pg_dump, verifies that pg_restore can read its table of
contents, and never labels a failed/incomplete dump as successful. This is not a full
restore drill. Keep the original vault key and final configuration separately, and
copy both configuration and dumps off this machine (the existing Windows machine can
serve as a temporary destination until external storage is ready). No automatic backup
schedule or external SSD is provisioned by this guide.

### Update from Windows, then apply in Portainer

The maintainer has deferred backup setup until their backup drive is ready. The
update helper does not require or create a backup. It prepares an image and leaves
the running containers, private settings and database untouched. Applying it in
Portainer can run database migrations; Git history only restores source code.

One-time setup: in the Ubuntu SSH terminal, download the helper from main:

```sh
cd /opt/amorrp/source
git pull --ff-only origin main
```

Save [Update-AmorRP.bat](../../deploy/home/Update-AmorRP.bat) on Windows, open it
in Notepad, and replace `REPLACE_WITH_YOUR_SSH_TARGET` with the same SSH target used
for the existing Ubuntu login. Keep that configured copy local. It contains no
password or Portainer API key and uses the existing SSH key/agent.

For each update:

1. Double-click the batch file. Enter the Ubuntu sudo password if prompted.
2. The helper fetches main and requires its latest **Server and contracts** workflow
   run to pass for the exact commit. If checks are running/failed or GitHub cannot
   be reached, it stops. It also refuses local edits and divergent history.
3. It fast-forwards source and builds a separate local image tagged with the server
   version and source commit. Build failures leave the running service in place.
4. Once it prints **BUILD READY**, copy the displayed image name into Portainer:
   **Stacks → amorrp → Editor → Environment variables → AMORRP_SERVER_IMAGE**.
   Keep all other values. Click **Update the stack** with image re-pull disabled.
5. Confirm public `/health/ready` returns `status: ok`, check the version at
   `/api/v2/capabilities`, and reconnect in game. Plugin updates still use Dalamud.

The same preparation can be run directly over SSH:

```sh
python3 /opt/amorrp/source/deploy/home/prepare-update.py
```

No NPM restart or new OAuth credentials are needed for an ordinary application
update. Keep the displayed source/image IDs and the previous image until the update
is verified. Roll back only when compatible with the database schema. Stack-definition
or infrastructure changes still need their own instructions; this helper builds the
backend and does not silently replace the Portainer configuration.

**Never select Remove volumes when deleting/redeploying the stack unless intentionally
wiping that installation.** A surviving data volume and surviving vault key are both
needed for recovery. This fresh-install cutover does not authorize future data wipes.

## Preparation verification

The prepared configuration passes Docker Compose validation for required credentials,
no published database/API ports, isolated persistent database storage, exact single-hop
proxy trust and bounded logs. Secret generation is tested for 0600 permissions, valid
32-byte keys, no secret output and refusal to overwrite existing configuration.

In the disposable build workspace, the home stack's PostgreSQL started from an empty
volume. The current backend applied every migration and reached readiness/capabilities
with the non-Render configuration. A generated backup restored successfully into a
separate PostgreSQL 18 database. These checks used dummy identity-provider credentials;
they do not prove live XIVAuth or public NPM routing. The local Docker image build was
blocked by the workspace's Microsoft image-download allowlist; the existing server CI
builds that pinned Dockerfile on GitHub. The maintainer's host still needs the steps
above. No home-server access, data migration or live endpoint switch was performed.
