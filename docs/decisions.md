# Current product decisions

Maintainer direction through 2026-11-16. This register records current decisions;
older conflicting proposals are summarized in [history](delivery/history.md).

| Area | Decision |
| --- | --- |
| Identity | Provider-verified character is the unit of ownership, permissions and weekly budgets. Do not correlate alts or identify assets by mutable name/world. |
| Seasons | Seasons replace Communities. Every verified player belongs to every non-deleted season automatically; economies remain separate. No join, leave, removal or season block controls. User bans are separate account moderation. |
| Creation | `seasons.create` is a global permission. Configured Lodestone administrators receive it by default and alone grant/revoke it or activate/archive seasons. Ordinary users do not see creation controls. |
| Lifecycle | One active season. New seasons are drafts with fresh economies; prior assets remain in read-only archives. Preserve existing data through explicit migrations. |
| RP groups | Owned groups inside a season retain independent membership and private content. Group feature approval, staff grants and gameplay authority are distinct. |
| UI | Dedicated Writing, Crafting, browsing, group/season management and Trade windows. Settings contains Interface/Account/About. Inventory companion sits to the right of vanilla inventory. |
| Launcher | Book opens Writing. Closed-eye icon toggles automatic panels/HUDs without text and retains individual panel preferences. No community selector. |
| Text and chat | Plain text; preserve literal `<3`. Explicit item Use previews/posts the validated message. No startup chat permission checkbox/window. |
| Financial integrity | Whole-unit checked currency, reservations, durable receipts and idempotency. All money sources use existing debt settlement; private group data is not exposed by season moderation. |
| Hosting | Maintainer's private Ubuntu/Docker/Portainer/NPM host; secrets remain private. Keep `AMORRP_ADMIN_LODESTONE_ID`; no season-enable variable. |
| Development | Main-only source/publication. No legacy modes, fallback flows or old API aliases unless explicitly requested. Extract reusable code where duplication justifies it; do not replace feature services with speculative frameworks. |
| Release | Four-part 0.feature.0.patch versions. Publish exact Windows bytes after matching server and plugin CI pass. Never overwrite a published version with different bytes. Automated, game and operating acceptance are distinct. |

Accepted expansion requirements and unresolved defaults are in
[planning decisions](planning/decisions.md). S01/S02 game acceptance is recorded;
S03 and seasonal/window game acceptance remain pending. The maintainer permits
independent maintenance work; destructive production operations and missing
product decisions are not implied by that permission.
