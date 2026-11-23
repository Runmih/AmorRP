# Economy expansion

S01 permissions, S02 treasury, S03 billing, and S04 billing revenue are
implemented. See the [current rules](../product/rules.md) and
[milestone plan](README.md). S05 and S06 are implemented. In-game acceptance is pending.

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

In 0.6, add **Shop** as a top-level tab inside the existing main Amor RP window.
Place it immediately after **Home** and before **Inventory** in the shared tab
navigation.

Owner-managed products have a price, permitted buyer type, eligibility, stock or
purchase limits where needed, and a defined outcome. Authorized groups buy using
their treasury; characters buy using their available balance. Proposed initial rule:
shop purchases require real available currency and do not create borrowing.

Add typed item-specific outcomes as needed; do not build arbitrary scripts or a
general rule/workflow language. Existing ordinary inventory remains usable. Shop
purchase and its item/document outcome commit together with replay-safe receipts.
Purchase proceeds credit the season treasury. An authorized group purchase delivers goods to the buyer’s character inventory.

Planned products include a default Alchemy class-reset potion. S06 adds a **Slave**
tab within the Season Shop. The tab lists available characters and their prices.
Registration issues a permit in the character's name. A group can hold that record
without general group item storage.

Verification: simultaneous purchase, failed delivery, changed price, currency scope,
unauthorized buyer, duplicate request and sufficient funds after reservations/debt.

## S06 — Ownership permit lifecycle (0.7)

### Public listings

A season moderator with listing permission can request a listing for a verified
character without an active permit. **Allow listing** publishes the character's
name and price. The character can withdraw until someone buys the listing.
Characters can be listed regardless of their debt balance.

The **Slave** shop table shows **Slave name** and **Price**. The price includes
the season's base price and the character's outstanding season debt. An eligible
group owner buys from the row, using their group treasury. The purchase registers
the permit and applies the owning group's Trusted billing lock.

### Private offer

From the character's trade actions, a group owner can choose **Enslave** to send a
private offer naming their character as the buyer and their group as the owner.
This route does not create a public listing. Both players review the named buyer,
owning group, total cost, billing lock and asset transfer, then explicitly
confirm. Either can withdraw the offer before settlement. The buyer's character
receives the agreed assets and the named permit is registered to their group.
Direct individual permit ownership would require its own funding, billing and
release rules.

The proposed private offer includes the character's available, transferable season
items and currency at settlement. Existing trade reservations and item transfer
rules apply. Show the exact assets on the offer. Changes to the asset list or price
require a fresh preview and confirmation. Issue the permit through the registration
transaction so the offer cannot turn into an ordinary item transfer.

### Registration and release

For private offers, show the owning group, cost breakdown, treasury balance and
any shortfall. For public listings, recheck the listing version and displayed
price when the buyer clicks Buy. Both paths verify eligibility and funds at
commit. Insufficient funds or changed terms leave the purchase uncommitted.

In a single transaction, pay existing creditors, register the named permit,
transfer the confirmed assets for a private offer, remove any public listing, set
the owning group/tag and lock Trusted on for that group. Record a replay-safe receipt.
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

Verification: listing permission, missing or withdrawn consent, private buyer
identity, concurrent buyers, changed quotes or assets, reserved items, insufficient
funds, racing debts, duplicate registration, forged permit IDs, unauthorized
deletion, repeated release, disbanding, and correct state when Moodles is absent.
