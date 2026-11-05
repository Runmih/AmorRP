# Amor RP 0.3.0.0

The maintainer confirmed on 2026-11-05 that disbanded groups' unpaid claims remain
owed and later repayments belong to their former owners through Treasury returns.
S02 (0.2.0.0) passed all four maintainer game checks on 2026-11-05.

## Changes

- Characters can mark a group Trusted for automatic billing from its profile. A trusted
  group can charge for services while the customer is offline; no per-bill payer action.
- Approved treasury owners can delegate Bill trusted customers under Staff. Billing
  and financial record controls remain hidden without the relevant authorization.
- Group Billing lets cashiers find community customers, enter a service and amount,
  charge the customer and view receipts. Each receipt shows amount, paid and still owed.
- Home → Your bills and debt shows personal receipts and total debt, with payment details.
- Home → Community Settings → Economy lets the community owner enable new debt. It
  starts off. Unaffordable bills then fail entirely. Existing debts continue to be paid
  when new debt is turned off.
- Oldest unpaid bills collect all unreserved coins and future income. Trades, grants,
  withdrawals and released trade reservations use the same repayment path. Long queues
  hold funds until durable automatic continuation finishes.
- Bill IDs prevent repeat charging even when ordinary operation replay has expired.
  A thirty-second cashier/customer/group cooldown reduces accidental fresh charges.
- On disbanding: claims retain their original archived group and receipts;
  later collected coins appear as Treasury returns for the former owner. Collection
  is explicit and itself repays the owner's debts, avoiding automatic payment loops.

No taxes, cashier commission, clock-in, Discord bot or new earning source is included.

## Deployment

Deploy latest main on Render before testing the updated plugin. Normal migration
startup/pre-deploy applies `20261105164939_TrustedBilling`. Existing money, reservations,
profiles and memberships are preserved; debt and trust start off. Downgrade refuses
to discard financial records or held repayments. No data wipe or hosting move.

Plugin/server/minimum client are 0.3.0.0. Amor RP remains API v2, XIVAuth external API v1,
and Dalamud API 15. Login/provider source, scopes and callback configuration are unchanged.
Use the normal main feed once the verified Windows package is published.

## Verification

Local verification passes 335 tests: 191 PostgreSQL/server tests, 143 linked plugin
helper tests and one dependency-boundary test. Builds have no warnings or errors;
model consistency, documentation, startup and package checks pass.

Automated verification covers billing approval/trust/staff authority, offline customers,
affordable and unaffordable debt-disabled bills, oldest-first partial repayments,
trade reservations/releases, trade income, treasury credit, durable bill identity,
cooldown, departure/privacy, simultaneous cashiers, archived claims, bounded restart
continuation and migration preservation. Windows package and server CI checks gate main feed publication. In-game acceptance remains pending.

## Major in-game checks

1. Enable a group's treasury, have a customer enable Trusted on its profile, and let
   the group owner or authorized cashier charge an affordable bill. Check both balances.
2. With debt off, try an unaffordable bill and confirm nothing is charged. Turn debt on
   and confirm a partial payment plus the correct remaining debt.
3. Give that character coins through a trade or currency grant. Confirm debt reduces
   automatically and the group receives exactly the repaid amount.
4. Remove customer trust or staff billing permission, Refresh groups, and confirm new
   charges are blocked. Existing bills should remain visible and payable.
