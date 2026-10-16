# Amor RP 0.0.6.16

Plugin and server version: 0.0.6.16. Publish the server before distributing the plugin;
the plugin checks for server 0.0.6.16 or newer. Existing plugin installations remain
compatible with the updated server. The main custom-repository URL is unchanged.

- Plain-language action feedback, with debug identifiers in Details.
- Responsive inventory, members, crafting and trading layouts; item type icons.
- Current currency balance and available offer funds in trades.
- Server-side whole-group member search with scoped pagination.
- Local, Windows-user-encrypted private member notes (300 characters).
- Permanent nine-character group invitations, unlimited uses and owner revocation.
- Shorter reconnect delays with backoff and retained drafts.

Migration `20261016220000_PermanentInvitations` extends only usable invitations.
It does not revive expired, revoked or exhausted invitations. This data change has
no automatic inverse: a downgrade cannot recover former expiry/use limits. Owners
can invalidate any migrated invitation using Delete invitation.

Venue discovery and sub-groups are deferred for discussion. No live database wipe
or live server restart is part of this release's local verification.
