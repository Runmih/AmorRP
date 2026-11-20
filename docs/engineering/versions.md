# Versions and release boundaries

Source package/assembly version lives once in
[Directory.Build.props](../../Directory.Build.props) and is inherited by all projects.
The package validator and home updater read that same value. Do not hardcode a
second server version in capabilities or an obsolete milestone label in build tools.

ApiProtocol defines application API v2 and the minimum client/server floor 0.4.0.3.
Maintenance 0.4.0.4 preserves that protocol. XIVAuth independently uses external API
v1. ServiceCompatibility validates the advertised API/server/minimum-client tuple;
malformed, unsupported and too-old tuples fail closed. There is no API adapter or
alternative community mode.

The plugin's installer check uses the published AssemblyVersion in repo.json.
It pauses gameplay if the installed version is older or the feed cannot be
verified. The API support floor remains independent of the published version.
Clients below 0.4.0.10 predate this installer check.

| Dependency | Source of truth / current pin |
| --- | --- |
| .NET SDK | global.json — 10.0.401, latestPatch roll-forward |
| Target/runtime | Directory.Build.props — net10.0, C# 14, nullable and warnings-as-errors |
| Dalamud | scripts/dalamud-reference.json — API 15, official assembly 15.0.3.6, pinned archive digest |
| Database | deploy Compose files — PostgreSQL 18.6, pinned image digest |
| Server/test packages | Directory.Packages.props and project lockfiles; EF 10.0.12, Npgsql 10.0.3 |
| Image decoder | Directory.Packages.props — SkiaSharp 4.153.1 with pinned native Linux assets |
| Documentation tools | scripts/requirements-docs.lock.txt |

Restore with --locked-mode. Change pins intentionally and regenerate only affected
lockfiles; do not hand-edit locks or upgrade unrelated dependencies during cleanup.
The plugin excludes host DLLs and ships Contracts, Core, protected-data support,
license metadata and its exact icon. The validator inspects the actual ZIP.

## Publication

Use main only. Versions follow 0.feature.0.patch; immutable package bytes require a
new version when they change. Both Windows package and server/contracts CI must
succeed on the exact source SHA. Publish the downloaded Windows inner latest.zip,
record its source/build/digest in packages/*.json, and verify public feed/download
bytes. Never publish a Linux-built plugin ZIP. repo.json records what is actually
published; source versions do not prove a home-server deployment.

The home updater requires successful Server and contracts checks on latest main,
then builds an image for manual Portainer application. A publication commit must
therefore receive that check too. Existing images, volumes, credentials and vault
keys remain operator-controlled. See [development](../delivery/development.md),
[distribution](../delivery/plugin-repository.md) and [home deployment](../delivery/home-server.md).
