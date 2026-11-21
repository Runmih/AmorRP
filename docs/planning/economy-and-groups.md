# Economy expansion

S01 permissions, S02 treasury, S03 billing, and S04 billing revenue are
implemented. See the [current rules](../product/rules.md) and
[milestone plan](README.md). S05 and S06 remain planned.

## S04 — Taxes, cashier commission and clock-in

The season owner sets a tax from 0–100 percent, credited to the season treasury.
The group owner sets a commission from 0–100 percent, credited to the issuing
cashier. Calculate tax first, commission on the remainder, and credit the rest to
the group treasury. Both fractions round down. Each payment uses the difference
between the split of the total collected before and after that payment, so paying
in installments produces the same split as paying in full.

Bills keep their rates and issuing cashier. Repayments use those values after
rate changes, clock-out, or staff leaving. Commission income enters ordinary
debt settlement. Deposits and withdrawals incur no service tax. Receipts show all
three credits.

Clock-in is a persistent group staff state. Group owners can require it for
billing and clock staff out. Losing billing access clears clock-in. In-game
verification is tracked in the [acceptance checklist](../delivery/acceptance.md).

## S05 — Season shop

Owner-managed products have a price, permitted buyer type, eligibility, stock or
purchase limits where needed, and a defined outcome. Authorized groups buy using
their treasury; characters buy using their available balance. Proposed initial rule:
shop purchases require real available currency and do not create borrowing.

Add typed item-specific outcomes as needed; do not build arbitrary scripts or a
general rule/workflow language. Existing ordinary inventory remains usable. Shop
purchase and its item/document outcome commit together with replay-safe receipts.
Resolve whether revenue is a sink or an explicit season account before shipping.

Planned products include a default Alchemy class-reset potion and ownership documents.
A group-owned registered document does not require general group item storage first.
Each regulated kind declares permitted transfer/deletion/use behavior on the server.

Verification: simultaneous purchase, failed delivery, changed price, currency scope,
unauthorized buyer, duplicate request and sufficient funds after reservations/debt.

## S06 — Ownership permit lifecycle

Initial registration requires explicit OOC acceptance from the affected character.
Show the owning group, document price, all current debts, total cost, treasury
available balance and any shortfall to the authorized group owner. Recheck the quote
and debt state at commit. If costs change, provide an understandable refreshed quote.
Insufficient funds cause no partial payment, document or status change.

Registration pays existing creditors through settlement, purchases/registers the
unique permit, sets the owning group/tag and locks Trusted on for that group.
Prevent overlapping active ownership registrations for the same season/character.
The document is an authoritative group-owned record, not a client-editable tag.

Only the owning group owner can destroy the active permit to release the character.
General item/treasury permissions must not bypass that rule through discard, trade,
consumption or warehouse endpoints. No self-service unlinking or automatic release
when a Moodle disappears. Season-owner override/settings can be discussed later;
existing group-disband authority still frees affected characters as required.

Permit deletion atomically ends the status and removes its billing lock. It unlocks
the customer's Trusted control; it does not silently erase debt, refund the original
purchase or reverse paid creditors. Disbanding voids all active permits and releases
their characters. Client-side Moodle removal is silent and best-effort afterwards.

Permit trading is deferred until registration, owner transfer, authorization, price,
debt treatment and billing-lock movement are specified together. Do not enable it by
merely marking an active permit tradeable in ordinary inventory.

Verification: missing acceptance, stale quote, insufficient funds, racing debts,
duplicate registration, forged permit IDs, unauthorized deletion, repeated release,
disbanding, and continuing correct state when Moodles is absent.
