# Development workflow

The repository builds a Dalamud plugin and an ASP.NET Core server. Start with the
[current architecture](../engineering/architecture.md), [source map](../engineering/repository-layout.md)
and [version matrix](../engineering/versions.md). Dated milestone guides preserve
historical installation/acceptance records.

## Prerequisites

- .NET SDK from `global.json` and locked NuGet dependencies.
- Pinned official Dalamud API 15 references for plugin builds. Fetch with
  `scripts/Fetch-Dalamud.ps1`, then set `DALAMUD_HOME` to its destination.
- Disposable PostgreSQL 18 for server integration tests. No hosted test server or
  real XIVAuth account is needed for automated tests; provider fixtures are injected.
- Python 3.12 recommended for documentation/package checks.
- Windows/FFXIV/Dalamud for manual game loading, rendering, input and chat checks.

Never run tests against the hosted database or use production secrets in fixtures.
`PostgresTestDatabase` requires an `amorrp_test...` database name and creates/drops
isolated databases for tests.

## Build and checks

With reference setup and `DALAMUD_HOME` complete:

```sh
dotnet restore AmorRP.sln --locked-mode
dotnet tool restore
dotnet build AmorRP.sln -c Release --no-restore
python3 -m pip install -r scripts/requirements-docs.lock.txt
python3 scripts/check-docs.py
python3 scripts/check-plugin-package.py
python3 scripts/check-plugin-repository.py
```

Start the local test database and configure this non-production connection:

```sh
docker compose -f deploy/compose.yaml --profile test up -d --wait test-database
export AMORRP_TEST_DATABASE='Host=127.0.0.1;Port=55432;Database=amorrp_test;Username=amorrp;Password=amorrp-local-only'
dotnet test tests/AmorRP.Contracts.Tests -c Release
dotnet test tests/AmorRP.Plugin.Tests -c Release
dotnet test tests/AmorRP.Server.IntegrationTests -c Release
ConnectionStrings__Database="$AMORRP_TEST_DATABASE" dotnet ef migrations has-pending-model-changes --project src/AmorRP.Server --configuration Release
```

The fixed Compose password is disposable development data. The helper and integration
test projects also have xUnit v3 executable runners; after building, their test DLLs
can be run with `dotnet path/to/TestAssembly.dll` when a sandbox blocks VSTest's
local communication socket.

## What the checks prove

- Contract tests check application/library dependency boundaries.
- Plugin tests compile the actual standalone transport, refresh, coordination,
  draft and text components. They cover token/character/context races and recovery;
  they do not instantiate the entire Dalamud panel or render ImGui.
- Server tests exercise real PostgreSQL constraints, authorization, transactions,
  migrations and concurrency through test-hosted HTTP endpoints with provider fakes.
- Documentation checks validate links, OpenAPI and endpoint/status metadata. They
  are not application correctness or security tests.
- Windows CI builds against verified Dalamud references and checks the distributable
  ZIP. Linux CI runs server/contracts/helpers, schema consistency and container checks.
  Both workflows have run successfully; every release needs fresh results for its source.

Tests for the renewal lock deliberately hold the provider response while unrelated
requests complete. Coordination tests use controlled task completions rather than
sleep-based timing. Manual in-game and real-provider acceptance remain separate.

## Change and release discipline

Keep changes bounded and explain the observed problem, resulting behavior and relevant
verification. Update current guides when behavior changes; dated release records are
historical evidence. Place rules requiring persistence beside the server transaction,
pure reusable rules in Core and concrete wire types in Contracts.

Do not change XIVAuth routes when changing Amor RP's protocol. Do not discard or
rewrite an unresolved saved mutation merely because a UI operation was renamed.
Preserve receipt fingerprints and test retries. Show debug IDs without exposing
credentials or provider responses.

Publish a new plugin version for changed package bytes. Use the exact successful
Windows build artifact, record its source/build URL, and verify the public feed and
package digest. Server-only corrections require a Render redeploy to become active;
local/CI testing does not require restarting the live service. Database migrations,
provider configuration changes and protocol changes are separate decisions.
