# External references

Reviewed 2026-10-06. Links establish platform capabilities or requirements, not a
successful Amor RP integration. Prices, APIs and policies must be rechecked at the
relevant milestone. Use primary documentation/source; do not rely on remembered
provider paths or snippets from unrelated plugins.

| Source | What it informs |
| --- | --- |
| [XIVAuth repository](https://github.com/XIVAuth/XIVAuth) | OAuth/device flows, character attestation, developer onboarding |
| [XIVAuth hosted service](https://xivauth.net/) | Selected identity provider |
| [XIVAuth developer agreement](https://xivauth.net/legal/devagreement) | Provider use and privacy requirements |
| [Render FAQ](https://render.com/docs/faq) | .NET via Docker, managed PostgreSQL, paid always-on compute |
| [Render WebSockets](https://render.com/docs/websocket) | WSS support; reconnect-aware live updates |
| [Render monorepos](https://render.com/docs/monorepo-support) | Shared source/build context and deployment filters |
| [Render free instances](https://render.com/docs/free) | Idle sleep, ephemeral filesystem, 30-day free database expiry |
| [Render PostgreSQL backups](https://render.com/docs/postgresql-backups) | Paid PITR; documented Hobby three-day recovery window |
| [Render DDoS protection](https://render.com/docs/ddos-protection) | Checked 2026-10-15: automatic upstream protection for hosted services; retain alongside caller limits |
| [Render public-service client IP](https://render.com/articles/host-pocketbase-on-render) | Checked 2026-10-15: recommends CF-Connecting-IP overwritten by managed edge; no static proxy list needed |
| [Render default environment variables](https://render.com/docs/environment-variables) | Checked 2026-10-15: RENDER=true is supplied automatically; used for ingress-mode detection |
| [Cloudflare request headers](https://developers.cloudflare.com/fundamentals/reference/http-headers/) | Checked 2026-10-15: visitor-IP header semantics and Worker subrequest caveats |
| [Render pricing](https://render.com/pricing) | Current plan/usage costs, to verify before provisioning |
| [Render cost breakdown](https://render.com/articles/how-much-does-cloud-application-hosting-cost-for-small-businesses) | Approximate smallest API + PostgreSQL baseline; extra storage/usage charges |
| [Dalamud technical considerations](https://dalamud.dev/plugin-development/technical-considerations/) | Windowing, TLS/DNS, data minimization, user discovery, retries/versioning |
| [Dalamud restrictions](https://dalamud.dev/plugin-publishing/restrictions/) | User-initiated game interaction, account-ID restrictions, subjective review |
| [Dalamud AI submission policy](https://dalamud.dev/plugin-publishing/ai-policy/) | Human review/testing/disclosure and human-written submission material |
| [Dalamud getting started](https://dalamud.dev/plugin-development/getting-started/) | Current SDK, build/metadata and distribution setup |
| [Dalamud stable release feed](https://kamori.goats.dev/Dalamud/Release/VersionInfo) | Checked 2026-10-06: stable assembly version 15.0.3.6, runtime 10.0.0; changes over time |
| [Dalamud versions and channels](https://dalamud.dev/versions/) | Current release API 15 and .NET 10; API compatibility and release-channel distinction |
| [Official sample project](https://github.com/goatcorp/SamplePlugin/blob/master/SamplePlugin/SamplePlugin.csproj) | Checked 2026-10-06: Dalamud.NET.Sdk 15.0.0; separate from runtime patch version |
| [Dalamud v16 preview](https://dalamud.dev/versions/v16/) | Next API is in development and not finalized; future adapter maintenance |
| [Dalamud v15 changes](https://dalamud.dev/versions/v15/) | API 15 services/chat changes, packager alignment and accurate ZIP manifests |
| [Dalamud SDK properties](https://github.com/goatcorp/Dalamud.NET.Sdk/blob/master/Dalamud.NET.Sdk/Sdk/Sdk.props) | net10.0-windows, C# 14, x64 and host-provided library references; verify the selected SDK artifact at M0 |
| [Dalamud project configuration](https://dalamud.dev/plugin-development/project-layout/) | Stable assembly/internal name, manifest and DLL naming |
| [.NET 10 download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) | Checked 2026-10-06: SDK 10.0.401 and runtime/ASP.NET Core 10.0.12 |
| [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) | .NET 10 LTS through 2028-11-14; current patch servicing requirement |
| [EF Core 10.0.12](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore/10.0.12) | net10.0 and matching Microsoft EF package versions |
| [EF migration tool 10.0.12](https://www.nuget.org/packages/dotnet-ef/10.0.12) | Matching local migration tool baseline |
| [Npgsql EF provider 10.0.3](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/10.0.3) | EF >=10.0.4 and <11.0.0, Npgsql >=10.0.3 dependency range |
| [Npgsql 10.0.3](https://www.nuget.org/packages/Npgsql/10.0.3) | PostgreSQL driver baseline; restore/live compatibility still needs tests |
| [PostgreSQL version policy](https://www.postgresql.org/support/versioning/) | Checked 2026-10-06: stable 18.6; major 18 support through 2030-11-14 |
| [Render PostgreSQL creation](https://render.com/docs/postgresql-creating-connecting) | Version 18 available/default; explicitly select major and record actual managed minor |
| [XIVAuth upstream routes](https://github.com/XIVAuth/XIVAuth/blob/main/config/routes.rb) | Source API v1 namespace; hosted compatibility still requires M1 validation |
| [Documentation validator metadata](https://pypi.org/project/openapi-spec-validator/0.9.0/) | Python >=3.10 dependency requirement, confirmed against installed package metadata |
| [Official Dalamud distribution at M0 pin](https://github.com/goatcorp/dalamud-distrib/tree/17c9393db64264f7e52f96ffa50aac3af19eb69e) | Actual API 15 reference archive downloaded and checksum-verified; pin in scripts/dalamud-reference.json |
| [Official development-loader instructions](https://github.com/goatcorp/SamplePlugin#activating-in-game) | Dev DLL path through /xlsettings and enable through /xlplugins |

External approval is not implied by technical feasibility. Context-menu and chat
behavior need real game testing. Official plugin distribution needs its own human
submission/review process. No messages to external maintainers were sent.
