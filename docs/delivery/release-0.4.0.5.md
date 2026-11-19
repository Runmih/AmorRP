# Amor RP 0.4.0.5 — Launcher switches and shared styling

The launcher power control uses FontAwesome PowerOff through the same icon-button
renderer and explicit size as every other launcher button. It replaces the smaller
plain button/custom eye drawing. It remains icon-only, with an on/off tooltip and
highlight, and preserves the saved attached-panel preferences.

Writing, Crafting, Season browsing, Group management, Season management and Trade
buttons now toggle their respective windows. Open windows highlight their buttons;
closing via X is reflected automatically. Opening goes through the existing feature
entry points; closing uses ordinary window lifecycle. Closing an accepted Trade
therefore cancels it as before, while invitation-only requests survive closure.
Context-menu/member/content links continue to open the requested destination.

PluginTheme owns the dark/gold palette, title bars, frames, tabs, buttons, popups and
scaled rounding. It is scoped around this plugin's WindowSystem.Draw and restored
with disposal, so Settings and Trade now match Writing, workspaces and attached
inventory. Duplicated per-window base styling is removed. Window identities,
placement, feature permissions and draft ownership are preserved.

The source version is 0.4.0.5. The API/minimum server floor remains 0.4.0.3; there is
no new database migration or deployment setting. An existing compatible home server
can continue running without an update for this UI change.

Verify in game: equal button sizes at different UI scales; Power icon/highlight and
panel preference persistence; each button opens then closes its window; X updates
its launcher highlight; Trade close cancellation and pending-request recovery;
consistent Settings/Trade/Writing/management appearance; no styling leak to other
plugins or ImGui stack errors. API 15 builds and automated regression checks do not
replace the final in-game appearance/lifecycle checks.

## Published verification

Stable **0.4.0.5** publishes source `5b895899433acb39031bf5fc671501014fcb7cd2`
from the exact [Windows artifact](https://github.com/Runmih/AmorRP/actions/runs/44624327206).
The matching [server/contracts workflow](https://github.com/Runmih/AmorRP/actions/runs/44624327185)
passed 190 server integration, 190 plugin helper and 1 contract tests, model parity,
documentation/deployment/startup checks and the server image build. The API 15
plugin build has no warnings or errors. Local helper/contract and ZIP/feed guards passed.

The downloaded artifact archive matches GitHub's digest. The inner package SHA-256
is `31902c9ce251ba4c60bdd7c7eb6245f7919f66d3cf94a0307b862f159806865a`;
its manifest reports 0.4.0.5 and Dalamud API 15. In-game visual and window-lifecycle
acceptance remains pending. No live home-server deployment was performed.
