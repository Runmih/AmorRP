# Amor RP 0.16.0.0

This local build brings together these five feature milestones.

| Milestone | Feature | Where to find it |
| --- | --- | --- |
| 0.12 | Discord writing rewards | Season Management → Economy, Home → Character, and the approved group’s Discord tab |
| 0.13 | Group item storage | Home → Groups → Storage |
| 0.14 | Permit transfers between groups | Shop → Slave → Registered permits |
| 0.15 | Staff shift scheduling | Home → Groups → Schedule |
| 0.16 | Individual cashier commission | Home → Groups → Members |

Storage accepts consumables and supply packs that have a definition and a revision.
Withdrawing requires a separate grant. Disband returns remaining stock to the
original group owner. A full receiving inventory prevents disband until the items
can be returned safely. Schedules display upcoming assignments and work alongside
the existing clock-in requirement. Individual rates apply to new bills, and their
receipts retain the rate used at issue.

A permit transfer moves the registration and billing lock after the affected
character approves and the receiving group owner accepts. The receiving treasury
pays the price to the current owning treasury. Existing debt stays with the
character, and the former group’s Trusted control becomes editable again.

The Writing Log, recipe editor, inventory tabs, and Season Management layout
have been simplified. Recipe emotes may use up to 200 characters within the
existing 200-byte game limit. Deleting a shop listing preserves purchases.

The plugin requires server 0.16.0.0. Back up the server database before upgrading.
The migration adds tables for the new workflows and keeps existing purchase,
billing, and permit records. Discord requires the optional [bot service](discord-bot.md).
