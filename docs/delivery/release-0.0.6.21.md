# Amor RP 0.0.6.21

## Changes

- A slow XIVAuth session renewal no longer holds the login lock shared by everyone.
  Other players can sign in, renew or revoke their own sessions independently.
- Command results use explicit operations and matching handlers, reducing the risk
  of unrelated forms or inventories reacting to an action.
- Late responses from an old character/community are discarded, including errors.
  Responses using an old access token wait for renewal before being evaluated.
- Recovering a saved action for another community acknowledges its receipt without
  overwriting the editor currently open.
- Current architecture, source navigation, authentication and development guides now
  describe the implemented system and distinguish historical plans.

## Deployment

Update the plugin from the existing main feed. Deploy the latest `main` server source
on Render to activate the renewal-lock fix. No new database migration, data wipe,
second service or XIVAuth configuration change is needed. Amor RP API remains v2,
XIVAuth's character API remains v1, and Dalamud API remains 15.

The plugin still accepts compatible server 0.0.6.18; the server continues to advertise
minimum client 0.0.6.18. Deploying the new server does not require changing the plugin
repository URL. A server restart was not required for local automated testing.

## Design and validation

Renewal/revocation use a database-wide per-session transaction advisory lock. Short
login admission/pairing transactions keep their global lock. A renewal still holds
its own connection/transaction while awaiting the provider, deliberately preserving
single-use credential rotation, same-key replay and revocation ordering. Provider
rotation and database commit are not distributed-transaction atomic; a lost provider
response can still require browser login.

`CommandRoutes` binds exact verbs/route shapes to operation, result handler and
refresh targets. Group activities expose typed service methods instead of a public
string/object dispatcher. Persisted request fingerprints and provider URLs are
unchanged. `RequestCoordinator` is used by the production panel and compiled into
helper tests, covering late results, cancellation, overlapping work and renewal races.

Focused local builds and tests passed at each repair stage. Final release checks
include the full contract/helper/PostgreSQL suites, docs and schema consistency,
Windows plugin packaging, and source/build provenance. CI provides the final package;
manual game rendering and live-provider acceptance remain separate checks.
