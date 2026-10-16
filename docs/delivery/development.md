# Development workflow

## Current setup reality

M0 replaces the legacy scaffold with Plugin/Core/Contracts/Server projects and
three test projects. .NET SDK 10.0.401 and real Dalamud API 15 references were used
to build the full solution. NuGet locks, tool manifest, pinned host archive, focused
ignores and CI workflows are present. PostgreSQL migration/readiness and 22 tests
passed locally. Windows game loading, hosted CI and container image build are
pending gates. See [exact installation and test commands](m0-install-test.md).

## Plugin version baseline

As checked on 2026-10-06, the stable release feed reports Dalamud **15.0.3.6**
(API **15**, .NET **10**). The official sample project uses
`Dalamud.NET.Sdk/15.0.0`; SDK package and running Dalamud patch versions are
separate version numbers. Use this as the recommended M0 implementation baseline.
API 16 is a development preview, not the initial stable target.

The plugin project now uses SDK 15 and compiles for Windows x64. The pinned
reference archive is version 15.0.3.6; live loading must use the compatible stable
track. See primary references in [sources](../sources.md).

The documentation checker is runnable now (Python 3.10+; Python 3.12 recommended):

```sh
python3 -m pip install -r scripts/requirements-docs.lock.txt
python3 scripts/check-docs.py
```

It checks local links, OpenAPI structure/reference resolution, authorization metadata,
operation-key requirements, endpoint catalog parity and feature/acceptance coverage.
It does not test server security or game behavior.

The old scaffold has been replaced; sample profile behavior was removed. Follow the full
[version matrix](../engineering/versions.md) for server, database, provider,
game/UI libraries, reproducible builds and updates.

## M0 prerequisites

- Development machine capable of current supported Dalamud/FFXIV plugin builds
  and a real game installation for manual tests.
- .NET 10 toolchain verified against the version matrix; Server/Core/Contracts
  target `net10.0`, plugin follows SDK 15's `net10.0-windows` x64 target.
  Verify exact tool/package pins, host references and locked restores at M0.
- Docker or disposable local PostgreSQL for server integration tests. Production
  database and credentials are never used in tests.
- GitHub CI access and local dev-plugin loading route. XIVAuth development app
  registration is required for M1, not for running M0.

## Implemented commands

First fetch references and set `DALAMUD_HOME`; configure the dedicated test DB as
shown in the installation guide. The following paths now exist:

```sh
dotnet restore AmorRP.sln --locked-mode
dotnet tool restore
dotnet build AmorRP.sln -c Release --no-restore
docker compose -f deploy/compose.yaml --profile test up -d --wait test-database
dotnet test AmorRP.sln -c Release --no-build --no-restore
python3 scripts/check-plugin-package.py
```

The installation guide provides OS-specific reference setup, database initialization,
migration invocation and plugin loading steps. The shell used for the development
task required network permission for .NET build/test process communication; this
is an execution sandbox constraint, not a normal developer prerequisite.
Use nonsecret `.env.example`/appsettings and user-secret/environment overrides.
Never require a contributor to copy production keys or query production data.

## CI and review

Implemented server CI restores locks, validates docs, builds/tests against real
PostgreSQL, checks migration-model consistency and builds the Docker image. Plugin
CI fetches checksum-verified references, builds/packages on Windows and validates
the ZIP before upload. Workflows have not yet run on GitHub. Presenter/business
rules and richer contract tests are added with their milestones. Live game tests
remain manual release gates.

Documentation pipeline: internal links, OpenAPI validation, unique operation IDs,
resolved schema refs, endpoint catalog parity, feature coverage and secret scan.
Do not use passing documentation checks as evidence of application correctness.

Each implementation change includes relevant rules/contracts/migrations, focused
tests of actual risk, and a reviewer-facing validation statement. No tests that only
mirror getters/setters or UI layout. Transactions/security/concurrency deserve tests;
simple display copy can be manually checked. Review SQL plans and locks for critical paths.

## Code conventions

Feature-oriented names and files, one authoritative rules implementation, nullable
checks, explicit cancellation, structured logging with request IDs, async I/O,
UTC clock abstraction and versioned configuration. No blocking requests in drawing.
Clean disposal unregisters commands/UI/context handlers and cancels subscriptions.
Use dependency injection at composition, not a global mutable service bag for rules.

No database entities in transport DTOs; no HTTP concerns in Core; no game APIs in
server libraries. Credentials use protected stores, not plugin JSON settings.
Do not accept floating-point money, guessed character identity, arbitrary URL fetches,
or dynamic execution from item content. Update docs whenever behavior changes.
