# Amor RP 0.3.0.1 — home-server candidate

This is a plugin-only hosting patch. It is prepared on main but **not published to
the installation feed** until the maintainer's new backend is ready.

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

## Publication gate

1. The maintainer deploys the home API and persistent database using the prepared stack.
2. Public HTTPS `/health/ready` returns ok and `/api/v2/capabilities` reports API2/server0.3.0.0.
3. The exact new XIVAuth callback is registered and configured in the stack.
4. Publish the verified Windows 0.3.0.1 artifact from the matching main commit, with
   immutable provenance. The main Dalamud feed remains on 0.3.0.0 until this step.

## Major live checks after publication

1. Fresh XIVAuth login, character verification and pairing succeed on the new host.
2. Create/join a fresh test community and complete a two-character trade.
3. Restart the API stack and confirm that the community and balances persist and
   reconnect still works. Retain the database volume and the original vault key.

No live home deployment or provider configuration edit is performed by this candidate.
