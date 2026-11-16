# Plugin interface

Current behavior is implemented in feature-owned screens and the shell; see the
[source map](../engineering/repository-layout.md). Native rendering/scaling and
multi-character interaction still require [in-game acceptance](../delivery/acceptance.md).

| Window | Entry and purpose |
| --- | --- |
| Settings | `/amorrp` or `/amorrp settings`; Interface, Account, About |
| Writing | Launcher book or `/amorrp writing`; full document list and dedicated reader |
| Crafting | `/amorrp crafting`; authorized Alchemy/Cooking and writing production |
| Season browser | `/amorrp season`; overview, Members, Groups, Noticeboard |
| Group management | `/amorrp groups`; owned/authorized group settings and staff features |
| Season management | `/amorrp manage`; authorized profile, permissions, economy and administration |
| Trade | `/amorrp trade`, member action or character context menu; requests and exchange |
| Inventory companion | Attached to the right of the selected vanilla inventory window |

The quick launcher has no community selection. Its book opens Writing; its
closed-eye icon toggles automatic panels/HUDs without text. Individual inventory
and event-HUD preferences remain saved while panels are hidden.

## Shared season context

Every verified character automatically receives every non-deleted season. The
selector browses them; it does not join or leave them. Default to the active season
on login/rollover and preserve deliberate archive browsing across normal refreshes.
Show draft/archive read-only state. Do not display obsolete join/create prompts.

Only `seasons.create` holders see creation controls. Administrators can activate or
archive with typed-name confirmation and grant creation permission under Creation
access, with a reason. Normal players never see these controls. Season owners and
delegated staff see only their relevant scoped management tools. No season
membership removal/block/restore or inactive-member controls remain.

## Data, drafts and feedback

Inventory supports tabs, categories, search, sorting, item details, pins and bounded
pagination. Finances appear at the bottom of the companion. Its filters/selection
are independent of the main inventory editor and Writing reader. Future Supplies
and Documents entries are honest empty states rather than unimplemented actions.

Drafts survive polling. Switching member/group context protects unfinished edits.
Permission updates reload after confirmed writes and keep conflict/recovery details
available. Background reads do not reorder or overwrite active editing unnecessarily.

Connection feedback and ambiguous operations live in a compact footer with details.
Keep the original operation key and request for recovery. Checking/retrying a saved
operation never repeats game chat. Do not reinterpret an unknown old command as a
new action. Authentication mismatch blocks feature use while preserving assets.

Account uses verified XIVAuth browser pairing. Interface preferences work while
signed out. About reports installed/last-connected versions without credentials.
The release endpoint is fixed in ProductServer; players do not edit hosting URLs.

## Feature details

Writing reads only currently owned private bodies. Creation/edits use bounded
wrapped text without adding soft-wrap characters to stored content. Trade requests
are separate from accepted exchanges; arrows/amount edits save offers, both
approval stages are revision-bound, and closing accepted exchange cancels safely.
Billing/treasury controls require their approved group permissions. Manage Groups
never exposes group-private content. Financial success/failure leaves a durable
receipt and accurate available/reserved balances.
