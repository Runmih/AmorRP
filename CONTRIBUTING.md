# Developing Amor RP

Start with [M2 build, installation and tests](docs/delivery/m2-install-test.md).
Use .NET SDK 10.0.401, PostgreSQL 18, and the pinned official Dalamud references.
The four application projects are under `src/`; automated tests are under `tests/`.

Core contains product rules and no game/HTTP/database dependencies. Contracts
contains transport types. Server owns persistence and endpoint composition.
Plugin owns game adapters, UI and local networking. Put code beside its feature;
do not create placeholder folders for unimplemented future features.

Update rules/contracts/acceptance notes alongside behavioral changes. Use real
PostgreSQL for transaction tests; never test against production. The integration
runner requires `AMORRP_TEST_DATABASE` naming a dedicated `amorrp_test...` database
and creates/drops its own isolated databases. No tokens or connection secrets in
source, logs or issues. The fixed Compose password is local development data only.

Do not edit generated package locks manually. Change a package pin, restore the
affected project, then use locked-mode restore/build/tests before reviewing it.
Migrations are explicit and version-controlled; CI checks model consistency.
See [versions](docs/engineering/versions.md) for maintenance policy and
[layout](docs/engineering/repository-layout.md) for navigation.
