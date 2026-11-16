# Product direction

- Seasons replace Communities. Ship one seasonal model with RP groups inside it.
- Do not introduce legacy modes, compatibility switches, fallback product flows or
  old API aliases unless the maintainer explicitly asks for them.
- Season creation requires the server-side `seasons.create` permission. Configured
  Lodestone administrators receive it automatically. Hide creation controls from
  characters without it and enforce the same rule on the server.
- Preserve existing player data through migrations. Migration history and stored
  records are data preservation, not a reason to retain superseded runtime behavior.
- Every verified character belongs to every non-deleted season automatically, including
  drafts and archives. Season membership has no join, leave, removal or block controls.
  User bans belong to account moderation, separate from seasons.
- Keep the Lodestone administrator identity setting. Seasons do not need an enable
  variable.
- Develop and publish on main. Publish the exact Windows artifact only after both
  matching source workflows pass; update the public installer feed and verify it.

# Maintenance boundaries

- Directory.Build.props owns the release version; ApiProtocol owns the wire-support floor.
- Keep one current documentation set and milestone plan. Condense superseded detail
  into history instead of appending conflicting instructions to current guides.
- Preserve saved JSON keys, DPAPI purposes, ImGui IDs and applied migrations during
  refactors. Keep authorization, lock and receipt ordering explicit in feature services.
