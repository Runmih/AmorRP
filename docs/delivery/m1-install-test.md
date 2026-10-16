# Install and test M1

M0 in-game testing was reported successful by the maintainer on 2026-10-06.
M1 plugin version is `0.0.2.1`, server `0.0.2`, Dalamud API 15. Toolchain pins
remain those in the [version matrix](../engineering/versions.md).

## What this build provides

- Browser login through XIVAuth with S256 PKCE, one-use state and selection of
  the current character's exact name and **home** world. Only `character` scope
  is requested. No user profile, email, full alt list or game ContentId is collected.
- A five-minute authenticated identity/reconnect probe and sign-out. Credentials
  remain in plugin/server memory. The plugin clears them on character/server
  change, expiry, cancellation and unload. Server restart requires fresh login.
- A player context-menu entry that displays a captured name/home-world ID hint.
  It does not start a trade or authenticate the target.
- A real chat test with message preview, 50 text-element/200-byte bounds and one
  permission question at plugin startup. Emote, Say, Party, eight Linkshell slots and eight Cross-world
  Linkshell slots are explicitly selectable. Permission remains active across
  sends, message/destination changes and character/group/backend switches. It is
  adjustable in settings and is asked again at the next plugin startup. No consumption, automatic fallback or automatic repost occurs.

These are isolated feasibility spikes. They do not implement the durable
`/api/v1/auth/*` contract or authorize future product routes. No new DB migration
is required. Attempts expire after ten minutes; expired attempt replay records
are retained another ten. Storage caps are 256 attempts and 256 sessions;
all probe routes share a limit of 180 requests/minute. Use one server instance.
Five-minute session expiry bounds verification staleness; there is no refresh or
ownership revalidation within that period. Durable ownership/revocation and
rename/transfer/unlink policy remain gates before product authentication ships.

## Install now: context-menu and chat tests

Replace the M0 plugin with the extracted `AmorRP-M1-plugin.zip` using the same
[development loader procedure](m0-install-test.md#install-the-plugin-artifact).
Disable/unload before replacing files. Keep all four package files together.
Run `/amorrp` and confirm the window identifies M1. An existing saved server
address continues working; no saved credentials are introduced.

With XIVAuth disabled, local health/readiness checks still work. Login reports
unavailability; it cannot pretend to authenticate you. You can test the context
menu and chat without a server or XIVAuth account.

1. Right-click another player: select **Amor RP: inspect character (M1)**. Confirm
   the exact name and home-world ID, including during world travel. Check player
   objects, party/friend lists and any other menus you expect to use. NPC and
   inventory menus must not show a character entry. Menus with insufficient
   character evidence are deliberately omitted; record any missing player case.
2. Answer the startup chat permission question once. Choose **Allow for this
   startup**, select Emote, inspect the preview and click **Post test message**.
   Verify the resulting text/channel. Send a second message: permission remains
   granted without another question.
3. Repeat for Say, Party (also without a party), each joined Linkshell and each
   joined Cross-world Linkshell slot. Verify the **actual** destination; labels
   show slot numbers, so consult the game's channel names before sending.
4. Select an unjoined slot: the game may reject the message; it must not be sent
   to a different channel. There is no programmatic delivery confirmation.
5. Change text/destination and switch character/backend: startup permission
   stays active and no permission question repeats. Disable posting in the chat
   setting: the send button must stop working. Enable it manually to continue.
   Reload the plugin: the startup question must appear once again. Decline it:
   chat posting stays disabled while the other diagnostic features remain usable.
   Check 50/51-character boundaries, accented text, emoji, percent signs and
   repeated clicks. Newlines, bidi/control formatting and `<macro>` placeholders
   must be rejected. Emote automatically includes the character name in-game;
   the preview shows the submitted command body rather than fabricating that name.
6. Unload/reload while checks/login requests are running. Log out/back in. Confirm
   no stale context subscription, secret display or automatic chat post.

Record Windows, FFXIV build, Dalamud track/version, plugin version, source commit
and each tested menu/channel. Do not mark unjoined channels as tested successfully.

## Set up Render

Nothing has been provisioned by this task. The Blueprint uses paid service/database
tiers; review their prices and approve them yourself before creating resources.

1. Select branch `m1-feasibility` in [the repository](https://github.com/Runmih/AmorRP).
   Use `deploy/render.yaml` as the [Blueprint path](https://render.com/docs/blueprint-spec), or create equivalent Docker web
   and PostgreSQL services in the same region. Dockerfile: `deploy/Dockerfile`;
   build context: repository root. Keep one web instance and automatic deploy off
   for this spike.
2. Set `ConnectionStrings__Database` to a **Npgsql/ADO.NET connection string** for
   that dedicated Render PostgreSQL database. A `postgresql://...` URL is not
   accepted directly by our app. Convert it into `Host=...;Port=5432;Database=...;`
   `Username=...;Password=...` using Render's database details and the required
   TLS options. Store it only as a secret environment variable. Use verified TLS
   for external database connections and confirm Render's selected connection
   path/certificate requirements. Do not reuse the local development password.
3. Pre-deploy command: `dotnet AmorRP.Server.dll --migrate` (included in template).
   Migrations must finish before `/health/ready` can pass. Start is the Docker
   entrypoint; Render's `PORT` is honored. No database port needs public exposure.
4. Deploy with `XivAuth__Enabled=false`. Check the public HTTPS `/health/live`,
   `/health/ready` and `/api/v1/capabilities`. The version should be `0.0.2`.
5. Copy the actual public service origin, such as `https://your-service.onrender.com`.
   The exact callback will be that origin plus `/auth/xivauth/feasibility-callback`.

The Blueprint/pre-deploy execution and full image build require live Render/CI
verification. They have not been represented as completed deployment evidence.

## Register XIVAuth and enable login

Use [XIVAuth](https://xivauth.net/) and its developer area. The maintainer must
complete account MFA/character verification and accept the developer agreement.
The authoritative flow/claims reviewed are pinned to upstream commit
[`4bc2440`](https://github.com/XIVAuth/XIVAuth/tree/4bc2440684989cf8e56bc1169afcf5bd3a200172).
Hosted-service behavior must still be checked against this source-based adapter.

1. Create an application and a **confidential OAuth client** supporting
   **authorization_code** and only **character** scope. Register the exact HTTPS
   callback above. No `character:all`, `character:manage`, user/email or refresh
   scope is needed. PKCE S256 is always sent.
2. In Render's secret/environment settings, set:

   ```text
   XivAuth__Enabled=true
   XivAuth__ClientId=<OAuth client ID>
   XivAuth__ClientSecret=<OAuth client secret>
   XivAuth__CallbackUrl=https://your-service.onrender.com/auth/xivauth/feasibility-callback
   ```

   The client ID is the OAuth client identifier, not an unrelated application ID.
   Do not send the secret in chat or commit it. Restart/redeploy after updating
   options. Invalid enabled configuration prevents startup.
3. In-game, enter the public HTTPS service origin. Check connection, then click
   **Start XIVAuth login** and **Open XIVAuth in browser**. Consent to the current
   character. Return to the game after the browser says verification succeeded.
4. Confirm the verified display, click **Check authenticated reconnect**, then
   **Cancel / sign out**. A revoked/expired token must no longer authorize identity.

Login initiation sends only the selected name/world hints. Provider token exchange
and verified-character lookup occur on the server over fixed HTTPS endpoints;
redirects are disabled. The adapter requests filtered selected-character results
and requires a single exact match, `verified_at`, Lodestone ID and `persistent_key`.
Provider access tokens are discarded after lookup; no refresh grant is requested.
Provider-side authorization can also be revoked in XIVAuth's own account settings.

## Authentication exit checklist

- Correct selected verified character succeeds. A different/unverified character,
  denied consent, malformed provider response or provider outage fails clearly.
- A callback cannot be reused; polling with another attempt's credential fails.
  Polling/browser HTML contains no session token. Secrets and callback query
  strings must not appear in app, proxy, hosting or support logs. Application
  request/client logging is suppressed; confirm Render-side request logging too.
- Reconnect succeeds before five minutes; expiry, logout and server restart require
  fresh login. Change game character/backend during browser login: old completion
  must never appear as the new character's session.
- Demonstrate provider revocation and unlink/relink; record hosted behavior and
  ownership key changes. Provider identity derivation is documented; normal durable
  authentication tests must verify ID/key resolution and ownership authorization.
  Live paid rename/transfer tests are waived by A16 and recorded as skipped for
  cost, not passed; no separate simulated-rename test is required. Do not require
  paid services to complete M1 or release; keep explicit recovery for changed bindings.
- Complete actual context-menu and channel evidence above. Only then accept M1.

## Build, automate and package

From a Windows checkout, run the reference fetch and locked restore commands in
the M0 guide, then build/test. Tests use the same disposable PostgreSQL setup:

```powershell
dotnet build AmorRP.sln -c Release --no-restore
docker compose -f deploy/compose.yaml --profile test up -d --wait test-database
$env:AMORRP_TEST_DATABASE = 'Host=127.0.0.1;Port=55432;Database=amorrp_test;Username=amorrp;Password=amorrp-local-only'
dotnet test AmorRP.sln -c Release --no-build --no-restore
python scripts/check-plugin-package.py
dotnet publish src/AmorRP.Server/AmorRP.Server.csproj -c Release --no-restore -p:UseAppHost=false -o artifacts/server
python -m pip install -r scripts/requirements-docs.lock.txt
python scripts/check-docs.py
python scripts/package-milestone.py --milestone M1
```

The Windows CI artifact is named `AmorRP-plugin`. Bundles are
`AmorRP-M1-plugin.zip`, `AmorRP-M1-server.zip`, `AmorRP-M1-source.zip`, plus
`SHA256SUMS-M1.txt`. No Dalamud host DLLs or secrets are bundled.

Automated evidence: complete Release build, zero warnings/errors; 53 tests
(34 plugin, 18 server including four PostgreSQL cases, one contract boundary).
Tests use an injected provider **only in test code**; there is no runtime mock
login mode. A successful test fixture is not a successful live XIVAuth login.
Acceptance update 2026-10-07: the maintainer reported M1 passed. This historical
guide records M1 build-time evidence; M2 uses the new [upgrade guide](m2-install-test.md).
