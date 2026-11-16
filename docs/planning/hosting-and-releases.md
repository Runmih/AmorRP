# Hosting and release policy

The maintainer's private home host is in use; the cutover is complete. Current
instructions are in [home deployment](../delivery/home-server.md),
[operations](../delivery/operations.md) and [versions](../engineering/versions.md).

Use main only for source and distribution. S01–S11 are work identifiers, not branches.
Versions follow 0.feature.0.patch. Documentation-only work does not invent a binary
version; changed published bytes require a new immutable version. The application
protocol remains v2; XIVAuth owns its independent v1 API.

Publish the exact verified Windows package after both matching source CI workflows
pass. The feed/provenance record must identify the actual source and artifact.
The server updater requires latest-main verification and prepares a local image;
Portainer application and private settings remain an explicit operating step.

Keep database/keys/identity bindings intact through normal updates. Backup restore,
capacity measurements and live game acceptance are separate evidence. Do not
provision paid infrastructure, deploy the private host, reset data or publish private
configuration as a side effect of development. Future Discord work may use the
existing permanent host; readiness is a prerequisite, not proof the bot is implemented.
