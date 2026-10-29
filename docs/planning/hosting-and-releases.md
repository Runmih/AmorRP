# Hosting and release policy

Maintainer decisions recorded 2026-10-29. This is a migration/release plan, not a
claim that the private server is installed, reachable or carrying live data.
Current plugin/server release is 0.1.0.0; private-server cutover is still future work.

## Branch and version policy

Use **main only** for ongoing development and publication. Do not introduce feature,
testing, milestone, release or separate stable branches. The S01–S11 work identifiers
are checklist references, not branches or version series. Do not publish a separate
Dalamud testing candidate as part of this plan.

Application versions use **0.feature.0.patch**:

| Part | Rule |
| --- | --- |
| First | Keep 0 |
| Second: feature | Increment for a completed feature release |
| Third | Keep 0 |
| Fourth: patch | Increment for fixes/adjustments within the current feature release |

The first feature release is 0.1.0.0; its fixes are 0.1.0.1, 0.1.0.2, and so on. The
following feature release is 0.2.0.0. Reset patch to 0 when feature increments.
The four-part numeric comparison makes 0.1.0.0 newer than 0.0.6.21. Do not release
unverified bytes or rename/reuse an already published immutable package version.

Keep plugin/server versions truthful to the binaries actually released; a plugin-only
patch does not require a fictitious server build. Record compatible/minimum client
versions explicitly. Documentation-only changes do not bump application versions.
Publishing policy is separate from HTTP protocols: Amor RP remains API v2 and
XIVAuth remains external API v1. No provider URL change follows from a feature number.

## Selected hosting target

The maintainer plans a private **Ubuntu server** on a GMKtec NucBox M8 with
AMD Ryzen 5 PRO 6650H, 16 GB RAM and 512 GB storage. Render remains testing
infrastructure until cutover. Use a supported Ubuntu Server LTS release and Linux
x64 builds. The exact Ubuntu release/network setup will be selected during setup.

The [listed configuration](https://www.digitec.ch/en/s1/product/gmktec-nucbox-m8-512-gb-16-gb-amd-ryzen-5-pro-6650h-pc-66735403)
is a reasonable initial target for the ASP.NET Core API, PostgreSQL and future bot.
This is a capacity estimate, not a benchmark, concurrent-user guarantee or certification
of this unit's Ubuntu drivers. Check network/storage support and sustained operation.

The existing [Dockerfile](../../deploy/Dockerfile) already supports linux-x64.
A small Docker Compose deployment is a proposed operational choice, not a requirement
to rebuild the applications. Keep the API, database and bot as separately managed
processes/containers on that machine, with persistent database storage, bounded logs
and automatic service restart. No additional paid test server is required.

## Cutover prerequisites

- Choose a stable public HTTPS hostname and ingress. If hosted on a residential
  connection, check ISP restrictions, upload reliability and CGNAT before assuming
  port forwarding works. A tunnel/reverse proxy is an option; direct public hosting
  can expose the household IP. Do not commit private addresses or machine identifiers.
- Keep PostgreSQL internal to the host/deployment network. Administrative access and
  application ingress have distinct access paths. Use wired Ethernet where practical.
- Back up PostgreSQL and the SecretVault encryption key set separately and off-machine;
  prove a restore. Lost encryption keys can make restored provider grants and protected
  receipts unreadable. Never copy production secrets into the repository or test fixtures.
- Configure the new ingress/client-IP trust explicitly. Current Render-specific mode
  depends on RENDER/UseRenderClientIp; do not copy it unchanged to Ubuntu or trust all
  forwarded headers. Verify separate clients receive separate rate-limit identities
  through the selected proxy/tunnel and direct backend access cannot spoof them.
- The plugin currently points to the Render origin in ProductServer. Coordinate an
  endpoint update with the new service; saved sessions and pending commands are
  origin-bound. Do not silently rewrite those bindings. Resolve pending outcomes
  before cutover and plan fresh login when the origin changes.
- Register/configure the exact new browser callback in XIVAuth when its URL changes.
  Preserve the provider's external API URLs and verification behavior. Test real
  browser login, pairing, renewal, logout and receipts before moving testers.
- Prevent split writes to independent Render/private database copies. Use a controlled
  write pause/final backup or an explicitly planned test-data wipe; no wipe is authorized
  by this document. Keep rollback and the old endpoint's treatment explicit.
- Verify automatic startup, PostgreSQL persistence, disk/log growth and monitoring.
  For a machine operated at home, power/router/ISP outages affect service availability;
  a UPS and off-machine backup improve recovery. One machine is not high availability.

Private hosting removes the Render-sleep dependency for a continuously running bot,
provided the machine/network actually stay on. It does not create the bot or validate
Discord application/intents. S11 still needs its own readiness and integration checks.

The maintainer will set up the server first. No deployment, DNS change, application
registration edit, database transfer, purchase or provisioning is performed by adding
this plan. Use [Ubuntu's server guidance](https://ubuntu.com/server) and the chosen
ingress provider's documentation when writing the concrete setup steps.
