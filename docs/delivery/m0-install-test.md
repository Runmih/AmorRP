# Install and test M0

Historical M0 build: use branch `m0-foundation` to reproduce these versions.
The maintainer reported M0 testing successful on 2026-10-06. For current work,
use the [M1 guide](m1-install-test.md).

Build date: 2026-10-06. Plugin version: `0.0.1.0`, Dalamud API 15.

M0 provides a loadable plugin package with `/amorrp`, local character/home-world
display, saved server address and an asynchronous connection check. The server
provides liveness, PostgreSQL/schema readiness and capabilities. Product features
start in M1–M4; M0 does not create accounts, groups, coins, potions, letters or trades.

## What was verified

- SDK 10.0.401 / runtime 10.0.12; all seven projects build in Release, zero warnings.
- Plugin compiled for Windows x64 against the actual official Dalamud 15.0.3.6
  reference archive; checksum pinned in `scripts/dalamud-reference.json`.
- 22 automated cases: 17 plugin network/origin cases, four real PostgreSQL
  bootstrap cases and one shared-assembly dependency check; no skipped cases.
- PostgreSQL 18.6 container; migration application, model consistency, published
  server start, all three response bodies against OpenAPI, and real database outage:
  liveness stays 200, readiness becomes 503 and recovers on restart.
- Locked NuGet restores; plugin ZIP identity/API/dependencies checked; Compose
  syntax validated. Docker image tags/digests and action commits are pinned.
- The packaged source was extracted into a fresh directory: locked restore,
  complete Release build, documentation checks and all 22 tests passed there too.
  The plugin DLL's PE architecture is verified as Windows AMD64.

Still pending: Windows CI execution, real FFXIV load/display/unload checks below,
and the server Docker image build. This environment can run PostgreSQL containers,
but its network policy blocks `centralus.data.mcr.microsoft.com`, used for Microsoft's
base-image downloads. The native published server was tested instead. Docker/CI
recipes are supplied, but those pending gates are not reported as passed.

## Prerequisites on your Windows PC

1. FFXIV started through XIVLauncher with plugins enabled; stable Dalamud API 15.
2. [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), baseline
   10.0.401. It includes the ASP.NET Core runtime required by the server.
3. Docker Desktop using Linux containers for the local PostgreSQL service.
4. The updated source checkout or extracted `AmorRP-M0-source.zip`. The source
   bundle has this build's changes even before they are pushed to GitHub.

In PowerShell, `dotnet --version` from the source root must resolve a compatible
10.0.4xx SDK. `docker version` must show both client and server. Start Docker
Desktop before running the database commands. No XIVAuth app/credentials are
required for M0.

## Start the local server using the prepared artifact

Extract `AmorRP-M0-server.zip` to a directory such as `C:\AmorRP-M0\server`.
Keep every extracted file together. From the **source root**, start PostgreSQL:

```powershell
docker compose -f deploy/compose.yaml up -d --wait database
```

If port 5432 is already occupied, change the Compose database host port and the
connection string together. Do not point these commands at a production database.
The included password is only for this local instance, bound to your PC's loopback
interface. PostgreSQL 18 mounts its volume at `/var/lib/postgresql`.

Open a separate PowerShell terminal in `C:\AmorRP-M0\server`:

```powershell
$env:ConnectionStrings__Database = 'Host=127.0.0.1;Port=5432;Database=amorrp;Username=amorrp;Password=amorrp-local-only'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5080'
dotnet .\AmorRP.Server.dll --migrate
dotnet .\AmorRP.Server.dll
```

The migration command exits successfully; the second command keeps the server
running. Leave that terminal open. Repeat migrations explicitly when a later build
adds them; normal server startup does not automatically change the database schema.
An initial EF migration-history lookup may be logged before the first table is
created; check the migration command's successful exit and readiness result.

In another terminal:

```powershell
Invoke-RestMethod http://127.0.0.1:5080/health/live
Invoke-RestMethod http://127.0.0.1:5080/health/ready
Invoke-RestMethod http://127.0.0.1:5080/api/v1/capabilities
```

Both health calls must return `status: ok`. Capabilities has API version `1`, server
version `0.0.1` and empty supported-type/capability/channel lists for M0.

Alternatively, build/start the entire stack from the source root:

```powershell
docker compose -f deploy/compose.yaml up --build -d server
```

This requires access to Microsoft's image registry/CDN. Compose starts the DB,
runs the migration service once, then starts the server on port 5080. This complete
container path awaits a successful image build outside the restricted environment.

## Install the plugin artifact

Extract `AmorRP-M0-plugin.zip` into a stable folder, for example
`C:\AmorRP-M0\plugin`. Keep `AmorRP.dll`, `AmorRP.Contracts.dll`, `AmorRP.json` and
`AmorRP.deps.json` together; adding only the main DLL loses its dependency.

In FFXIV:

1. Run `/xlsettings`. In **Experimental**, add
   `C:\AmorRP-M0\plugin\AmorRP.dll` to **Dev Plugin Locations** and save.
2. Run `/xlplugins`. Under **Dev Tools → Installed Dev Plugins**, enable **Amor RP**.
3. Run `/amorrp` to open its window.

These steps follow the official [development-loader instructions](https://github.com/goatcorp/SamplePlugin#activating-in-game).
This is local developer loading; no custom repository URL is published for M0.
Disable any older Amor RP build before loading this one so its internal name and
command do not conflict.

## Manual acceptance checklist

Record Dalamud version/track, FFXIV build, Windows version and plugin version.
Run these checks before considering the live M0 gate passed:

- Enable plugin; no load error in `/xllog`. `/amorrp` opens/closes the window.
- Logged-in character and **home world** are correct. If possible, repeat during
  world travel. Log out and back in: the window should handle missing player state.
- With the server running, keep `http://127.0.0.1:5080/` as the address and click
  **Check connection**. Expect the server/database-ready message without a UI freeze.
- Stop the native server with Ctrl+C; check again. Expect a readable connection
  failure. Restart it and repeat: connection succeeds.
- Stop the DB from the source root with
  `docker compose -f deploy/compose.yaml stop database`. With the native server
  still running, the plugin reports the database is unready. Start the DB again
  using `docker compose -f deploy/compose.yaml up -d --wait database`; check recovers.
- Try a public HTTP address or an address containing credentials/query parameters:
  the plugin rejects it. HTTPS and deliberate localhost HTTP are accepted.
- Disable/re-enable the plugin and restart the game: saved address survives.
  Disable it while a connection check is running: no error or hung unload.
- Verify no RP message is posted and no character/account data is sent during a
  connection check. This build makes only the three public GET requests.

If a check fails, record the step, visible message and relevant `/xllog` error.
Do not include passwords or tokens in reports. Live context-menu and chat-channel
integration are M1 tests; they are not available in this package.

## Build from source and run automated tests

From the source root, in PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Fetch-Dalamud.ps1
$env:DALAMUD_HOME = (Resolve-Path .tools/dalamud).Path
dotnet restore AmorRP.sln --locked-mode
dotnet tool restore
dotnet build AmorRP.sln -c Release --no-restore
docker compose -f deploy/compose.yaml --profile test up -d --wait test-database
$env:AMORRP_TEST_DATABASE = 'Host=127.0.0.1;Port=55432;Database=amorrp_test;Username=amorrp;Password=amorrp-local-only'
dotnet test AmorRP.sln -c Release --no-build --no-restore
python scripts/check-plugin-package.py
```

The last package check requires Python 3.12 (3.10+ supported). Tests refuse an
admin database whose name does not start with `amorrp_test`; they create/drop new
isolated test databases on that instance. Expected count for this build: 22.
The DB user needs `CREATEDB` in the dedicated test instance; Compose provides it.

Build outputs: `src/AmorRP.Plugin/bin/x64/Release/AmorRP/latest.zip` and
`src/AmorRP.Server/bin/Release/net10.0/`. To publish/recreate the prepared bundles:

```powershell
dotnet publish src/AmorRP.Server/AmorRP.Server.csproj -c Release --no-restore -p:UseAppHost=false -o artifacts/server
python -m pip install -r scripts/requirements-docs.lock.txt
python scripts/check-docs.py
python scripts/package-m0.py
```

`artifacts/` contains plugin/server/source ZIPs and `SHA256SUMS.txt`. Artifacts,
downloaded Dalamud references and build/cache outputs are ignored by Git.
Windows CI builds and uploads the plugin ZIP on each push/PR or manual run; server
CI validates documents, runs PostgreSQL tests and builds the Docker image. Workflow
files have been written; their hosted execution still needs a pushed commit.

## Cleanup and next milestone

Disable the dev plugin before replacing its files. Stop the server with Ctrl+C.
From the source root, `docker compose -f deploy/compose.yaml --profile test down`
stops/removes local containers while retaining the main development database volume.
Add `--volumes` only when deliberately resetting that local database.

M0 is ready for these installation checks. Its live-game/Windows CI/container
gates remain pending until their evidence is recorded. M1 then adds the real
XIVAuth identity, context-menu and consented chat feasibility experiments.
