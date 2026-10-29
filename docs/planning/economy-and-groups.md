# Economy and groups

Planned work S01–S06. Authority comes from [accepted decisions](decisions.md);
[the roadmap](README.md) supplies dependencies and progress. Proposed defaults
below remain feature-time choices, not implemented behavior.

## S01 — Permissions and authority

Keep three scopes distinct:

- Community owner: enables group features such as Treasury access and Discord
  Bot Permission, and sets community economic/gameplay policies.
- Group owner: assigns group staff permissions within enabled features.
- Character: marks a group Trusted for automatic billing. That switch does not
  grant the group Treasury access or moderation authority.

Organize the existing community permission UI into understandable categories without
renaming stable capability identifiers merely for display. Preserve Manage Gameplay
Permissions and its existing limits. Class choices must never grant moderation or
integration administration. Hide unavailable tabs; omit explanatory locked tabs.

Add narrowly scoped group staff grants for billing, financial records, treasury
withdrawal, income configuration and staff administration. Add item access only
with item storage. The group owner has their group's staff powers by default;
ordinary members cannot grant themselves features. Permission edits are authorized
and versioned on the server, not enforced only by hidden UI.

Community Manage Groups remains deletion-only. Approving a group's financial or
Discord feature does not expose its private roster or announcements. Scope any new
financial oversight to the financial function actually needed.

Verification: denied/cross-group grants; stale permission edits; owner versus staff;
feature revocation during an action; preservation of current permissions/privacy.

## S02 — Accounts, treasury and disband settlement

The current character balance is attached to a community membership. Extend that
accounting deliberately to group accounts; preserve integer amounts, available versus
reserved funds, ledger history and durable command replay. Do not implement treasury
as an editable number disconnected from the existing transfer/receipt system.

The initial treasury holds currency. Character accounts, group accounts, and any
explicit community revenue account need unambiguous owners and community scope.
Treasury access is individually approved by the community owner. Define deposit,
withdrawal and receipt behavior before exposing buttons.

All incoming character credits must have one authoritative settlement path by S03:
trade receipts, treasury withdrawals/disband returns, currency adjustments, shop
refunds, commission, gathering-item sales and later Discord earnings. Group credits
are not character income until paid to a character. Ordinary transfers must not
mint currency. Only explicit authorized issuance/reward sources may do that.

Disbanding rules:

- Stop new group actions under the same authority/transaction boundary as deletion.
- Transfer remaining currency and, once storage exists, items to the registered
  group owner in that community. Use that owner even when a moderator deletes it.
- Preserve asset provenance, item versions and reserved funds. Resolve reservations
  using existing cancellation/expiry rules before moving assets.
- Do not lose assets to inventory limits or an inactive owner membership. Define a
  recoverable delivery path if direct delivery cannot complete; do not silently discard.
- Journal settlement so retries/restarts cannot pay the owner twice.
- S03 adds creditor-claim succession; S06 adds permit release; S11 adds unlink/eject.
  Every feature must extend this common lifecycle rather than bypass it with raw deletion.

Verification: simultaneous withdrawals and deletion; replay after a lost response;
moderator-triggered deletion; inactive beneficiary; community isolation; assets and
reservation totals before/after. Later features must also check community deletion
and owner-membership removal paths that affect groups.

## S03 — Billing, debt and full collection

An authorized cashier bills an active community character who trusts that group.
The customer can be offline; they do not confirm each bill. Billing requires an
enabled group financial feature and staff permission. No arbitrary group can debit
an untrusted character. Community debt policy controls uncovered amounts.

Example: a 100-coin service with 30 available coins collects 30 and records 70 owed.
The treasury receives only collected proceeds. The debt is a receivable, not 70 new
spendable coins. If debt is disabled, define the failure behavior before shipping;
proposed default is to reject an unaffordable bill without a partial charge.

Use a stable bill ID and existing UUIDv7 request/receipt discipline. A repeated
submission returns its existing outcome or a clear recovery/expired-replay response;
it never creates a new bill because ordinary protected-response replay expires.
A short per-cashier/customer cooldown can reduce double-clicks, but it cannot replace
identity-based deduplication. A new deliberate bill remains a separate RP action.

Every bill/receipt identifies community, issuer, debtor, creditor, service, amount,
collected amount and remaining debt. Keep the original event and subsequent
settlements traceable; do not edit history to make debts disappear. Clerical
correction/cancellation details belong in the feature checklist, not a general
court/dispute system in this first version.

Collect full unreserved funds automatically, using a deterministic order. Oldest
bill first is the proposed starting order; rank configuration can be added if needed.
A newly received payment still counts as income/reward even when entirely diverted
to debt. Do not reset earning or crafting usage counters because debt absorbed it.

Debt collection and commissions can create chains or cycles. Do not recursively
invoke public billing/transfer commands from a credit hook. Design bounded settlement
that makes measurable progress, never creates fresh debt while collecting old debt,
and conserves money through rounding, retries and concurrent credits. If work is
continued durably, funds owed to collection cannot briefly become spendable twice.
This is an engineering design/verification gate before enabling bills.

When a creditor group disbands, do not erase its claims. Proposed default: keep the
same claim IDs/order and transfer its residual treasury entitlement to the former
owner, while honoring any recorded tax/staff allocation. Confirm the exact treatment
when S03 starts. Asset succession is not permission to invent a debt write-off.

Verification: offline billing, revoked trust, permission loss, exact/insufficient
funds, debt-disabled policy, partial repayments, reserved balances, simultaneous
bills/credits, priority ties, cycles, deletion, duplicate submission and old receipts.

## S04 — Taxes, cashier commission and clock-in

The group owner sets a commission percentage; the issuing cashier receives that
share and the treasury receives the remainder. No shift pool. Proposed basis:
community tax first, then commission on collected net proceeds. Numeric bounds,
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

## S05 — Community shop

Owner-managed products have a price, permitted buyer type, eligibility, stock or
purchase limits where needed, and a defined outcome. Authorized groups buy using
their treasury; characters buy using their available balance. Proposed initial rule:
shop purchases require real available currency and do not create borrowing.

Add typed item-specific outcomes as needed; do not build arbitrary scripts or a
general rule/workflow language. Existing ordinary inventory remains usable. Shop
purchase and its item/document outcome commit together with replay-safe receipts.
Resolve whether revenue is a sink or an explicit community account before shipping.

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
Prevent overlapping active ownership registrations for the same community/character.
The document is an authoritative group-owned record, not a client-editable tag.

Only the owning group owner can destroy the active permit to release the character.
General item/treasury permissions must not bypass that rule through discard, trade,
consumption or warehouse endpoints. No self-service unlinking or automatic release
when a Moodle disappears. Community-owner override/settings can be discussed later;
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
