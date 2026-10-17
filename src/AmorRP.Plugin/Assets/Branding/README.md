# Plugin branding

`logo-source.png` is the maintainer’s replacement red-and-gold RP artwork,
supplied in `amorRPlogo.zip` on 2026-10-16. It is already 512 × 512. `icon.png`
is an exact byte-for-byte copy; no resizing, recoloring or redrawing is applied.
It is the plugin installer icon, separate from community and currency icons.

To regenerate this icon, copy `logo-source.png` to `icon.png`.

After changing the icon, update the source manifest's IconUrl to the PNG's SHA-256
under the distribution branch's `images/` directory. Package checks validate the
square dimensions and URL/content match. Promotion publishes those same PNG bytes
and the manifest together, retaining old content-hashed icons.
