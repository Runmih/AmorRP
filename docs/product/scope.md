# Implemented scope and remaining work

Amor RP is a Dalamud plugin and authoritative server for RP seasons. Players sign
in with XIVAuth, automatically receive all seasons, manage fictional assets and
use dedicated feature windows. One season is active; other seasons retain separate,
read-only economies. The [milestone plan](../planning/README.md) owns delivery status.

| ID | Feature | Current scope |
| --- | --- | --- |
| F01 | Identity | Durable provider verification, pairing and session recovery; stable character ownership |
| F02 | Seasons | Automatic shared membership, guarded draft creation and administrator lifecycle |
| F03 | Permissions | Global creation permission, season gameplay grants, group feature/staff authority |
| F04 | Currency | Whole-unit personal accounts, adjustments, reservations, treasury and debt settlement |
| F05 | Types | Alchemy/Cooking consumables and book/letter/note writing |
| F06 | Categories | Season-owned organization and allowed types; no authority from category membership |
| F07 | Definitions | Authorized immutable recipe revisions, retirement and cost validation |
| F08 | Production | Character/season weekly quotas and atomic production |
| F09 | Writing | Shared writing allowance, owned text reader and untraded-author editing |
| F10 | Inventory | Search/filter/sort, item details, pins, finances and discard in a dedicated window |
| F11 | Use/chat | Explicit validated preview, one committed item use and chosen chat destination |
| F12 | Trading | Invitations, reservations, two-stage revision-bound confirmation and atomic exchange |
| F13 | History | Personal records, participant receipts and owner audit; private bodies excluded |
| F14 | Recovery | Durable idempotency, preserved pending requests and restart/reconnect recovery |
| F15 | Settings | Interface/Account/About, independent windows and panel visibility |
| F16 | Lifecycle | Version checks, migrations, home deployment and support; export/deletion and operating drills remain planned |
| F17 | Profiles/notices | Season/group profiles, logos, retained announcements, noticeboard and events |

Treasury and trusted billing are implemented S02/S03 extensions. S01/S02 have game
acceptance; S03/Seasons/window acceptance remains pending. Taxes, commissions,
shop, permits, gathering, classes, group recipes, Moodles and Discord earnings are
planned in the [expansion documents](../planning/README.md). Avoid placeholders or
unimplemented controls in player flows.

A final 1.0 needs the tested player experience, data integrity, private-data
lifecycle, documented operational limits and recovery evidence. An installed beta,
a clean build or an implemented OpenAPI entry does not establish those gates.
