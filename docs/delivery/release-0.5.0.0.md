# Amor RP 0.5.0.0

- Season owners set the tax rate and view the season treasury under Season Management → Economy.
- Group owners set cashier commission and optional clock-in under Group Management → Billing. Staff clock in and issue bills there. Group owners can clock staff out from Staff.
- Bill payments show the season tax, cashier commission, and group treasury credit. Repayments use the original bill rates, even after policy or staffing changes.

Apply the server update and let the database migration finish before publishing
the plugin in the installer feed. The 0.5 plugin requires the 0.5 server.
Current 0.4 clients continue working during the server rollout. The migration
preserves old bills and receipts and initializes tax and commission to zero.

See [home deployment](home-server.md), [installer publication](plugin-repository.md)
and [manual acceptance](acceptance.md).
