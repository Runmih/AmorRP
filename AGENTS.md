# Product direction

- Seasons replace Communities. Ship one seasonal model with RP groups inside it.
- Do not introduce legacy modes, compatibility switches, fallback product flows or
  old API aliases unless the maintainer explicitly asks for them.
- Season creation requires the server-side `seasons.create` permission. Configured
  Lodestone administrators receive it automatically. Hide creation controls from
  characters without it and enforce the same rule on the server.
- Preserve existing player data through migrations. Migration history and stored
  records are data preservation, not a reason to retain superseded runtime behavior.
- Keep the Lodestone administrator identity setting. Seasons do not need an enable
  variable.
- Develop and publish on main. Publish the exact Windows artifact only after both
  matching source workflows pass; update the public installer feed and verify it.
