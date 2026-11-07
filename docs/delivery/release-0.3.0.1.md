# Amor RP 0.3.0.1 — home-server cutover

This plugin-only hosting patch is published to the main installation feed on
2026-11-07. The backend remains 0.3.0.0.

- The plugin origin becomes the maintainer's selected public HTTPS hostname.
- The installation repository URL remains unchanged.
- Backend/minimum client/server remain 0.3.0.0, Amor RP API v2, XIVAuth external v1
  and Dalamud API15. No provider exchange logic, permissions or API routes change.
- The maintainer authorized a fresh database because the Render test data is no
  longer available. Nothing is imported or silently replayed into that empty database.
- Players sign in again; old sessions/pending commands remain bound to the old origin.

Deployment and host-side configuration are in the [home-server guide](home-server.md).
The public callback must be registered on the existing XIVAuth application as
`/auth/xivauth/callback` under the new hostname. Provider API URLs and scopes remain
unchanged. Keep client credentials and the vault key out of chat and source control.

## Publication checks

On 2026-11-07 the maintainer confirmed the home API/container deployment, healthy
PostgreSQL, public HTTPS readiness, API 2/server 0.3.0.0/minimum client 0.3.0.0,
and registration of the exact new XIVAuth callback. The matching source passed
both GitHub workflows. The Windows artifact from run 42115083628 is published
unchanged, with its source commit and SHA-256 recorded beside the package.
Actual browser login and in-game operations remain to be tested.

The release checks were:

1. The maintainer deploys the home API and persistent database using the prepared stack.
2. Public HTTPS `/health/ready` returns ok and `/api/v2/capabilities` reports API2/server0.3.0.0.
3. The exact new XIVAuth callback is registered and configured in the stack.
4. Publish the verified Windows 0.3.0.1 artifact from the matching main commit, with
   immutable provenance. The main Dalamud feed now offers 0.3.0.1.

## Major live checks after publication

1. Fresh XIVAuth login, character verification and pairing succeed on the new host.
2. Create/join a fresh test community and complete a two-character trade.
3. Restart the API stack and confirm that the community and balances persist and
   reconnect still works. Retain the database volume and the original vault key.

Host-side deployment and provider configuration were performed by the maintainer.
The feed publication does not restart or modify the deployed backend.
