# Economy expansion

S01 permissions, S02 treasury and S03 billing are implemented; see the
[current rules](../product/rules.md) and [milestone plan](README.md).
The following S04–S06 work is planned, not installed behavior.

## S04 — Taxes, cashier commission and clock-in

The group owner sets a commission percentage; the issuing cashier receives that
share and the treasury receives the remainder. No shift pool. Proposed basis:
season tax first, then commission on collected net proceeds. Numeric bounds,
rounding and tax recipient are explicit policies, settled before implementing S04.

Snapshot the tax/cut/payee allocation when issuing the bill. Later debt repayments
follow that allocation even if the cashier clocks out, leaves, or rates change.
Do not distribute unpaid debt. Staff income itself goes through full debt collection.
Deposits, withdrawals and repayments must not accidentally incur the same service
tax multiple times. Display receipts with a breakdown that adds up exactly.

Basic clock-in is a staff state, optionally required for billing by group policy.
Do not start with schedules, hourly wages, pooled payroll or per-staff rate tables.
Permissions for changing rates and correcting staff clock-in remain group-scoped.

Verification: 0/100-percent commission, rounding small payments, partial collection,
rate changes, cashier debt, departed staff and permission changes during billing.

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
