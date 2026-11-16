# Development workflow

Start with [AGENTS.md](../../AGENTS.md), the [milestone plan](../planning/README.md),
[architecture](../engineering/architecture.md) and [source map](../engineering/repository-layout.md).
Use main and the Runmih commit identity. Preserve data, applied migrations, saved
configuration keys and immutable packages. Do not introduce a legacy product flow.

## Setup

Use the SDK in global.json and locked dependency graphs. Fetch official pinned
Dalamud references with scripts/Fetch-Dalamud.ps1 and set DALAMUD_HOME to the verified
directory. Plugin builds need Windows references; in-game validation needs actual
FFXIV/Dalamud. Server tests use a disposable PostgreSQL 18 database, not live secrets.

```sh
dotnet restore AmorRP.sln --locked-mode
dotnet tool restore
python3 -m pip install -r scripts/requirements-docs.lock.txt
docker compose -f deploy/compose.yaml --profile test up -d --wait test-database
export AMORRP_TEST_DATABASE='Host=127.0.0.1;Port=55432;Database=amorrp_test;Username=amorrp;Password=amorrp-local-only'
export ConnectionStrings__Database="$AMORRP_TEST_DATABASE"
```

The integration fixture requires an amorrp_test... name and creates/drops its own
isolated databases. Fixture identity providers avoid real accounts and credentials.

## Verify the change

```sh
dotnet build src/AmorRP.Plugin -c Release --no-restore -p:Platform=x64
dotnet build src/AmorRP.Server -c Release --no-restore
dotnet test tests/AmorRP.Contracts.Tests -c Release --no-restore
dotnet test tests/AmorRP.Plugin.Tests -c Release --no-restore
dotnet test tests/AmorRP.Server.IntegrationTests -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/AmorRP.Server --configuration Release --no-build
python3 scripts/check-docs.py
python3 scripts/check-plugin-package.py
python3 scripts/check-plugin-repository.py
python3 scripts/check-home-deployment.py
python3 scripts/check-home-updates.py
python3 scripts/check-server-startup.py
```

Run focused checks while developing; run release checks for the final candidate.
Tests must exercise actual accounting, authorization, upgrade and recovery behavior.
Do not add tests that merely copy UI conditionals or incidental implementation shape.
Documentation validation checks links, API-source parity and acceptance coverage;
it does not execute application behavior.

## Structure and versions

Put rules beside their feature and transaction. Core owns pure reusable text/type
rules; Contracts owns transport and protocol constants. Shared media storage does
not own feature authorization. Avoid generic engines or new layers without a real
repeated use. Remove an obsolete adapter rather than stacking another translation.

Directory.Build.props owns the release version. ApiProtocol owns the independently
versioned wire-support floor. Never edit dependency locks by hand. Change a pin,
restore affected projects, then verify locked-mode restore. Schema changes need an
explicit migration and model parity; source-only cleanup does not need one.

## Publish and deploy

Push source to main and wait for both matching Windows/server CI workflows. Download
the exact Windows artifact, verify its outer digest, extract inner latest.zip and
validate that package. Use prepare-plugin-repository.py with package, destination,
source-commit and build-url. Commit feed/package/provenance and record release evidence. Every main push,
including publication, runs verification. The built-in-token publisher explicitly
dispatches the checks because its pushes do not emit normal workflow triggers. Verify plain
and cache-busted public feeds, download URLs, ZIP digest and manifest versions.

Do not publish Linux-built plugin bytes or overwrite a version's existing ZIP.
Do not deploy the private host as a side effect. Follow [home deployment](home-server.md)
for a manual server update and [acceptance](acceptance.md) for game/operating gates.
