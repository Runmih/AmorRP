# Dalamud repository and publishing

The stable feed is part of `main` in [Runmih/AmorRP](https://github.com/Runmih/AmorRP).
The source checkout, installer feed and immutable packages live in the same branch.
No milestone or separate distribution branch is required.

## Installation

In Dalamud Settings → Experimental → Custom Plugin Repositories, add:

```text
https://raw.githubusercontent.com/Runmih/AmorRP/main/repo.json
```

Install Amor RP from the Plugin Installer. Its internal name remains `AmorRP`,
so retain the existing plugin configuration when moving from another repository.
The new feed replaces the previous custom repository entry.

## Distribution files

- `repo.json`: generated stable plugin entry, version and package URLs.
- `packages/AmorRP-VERSION.zip`: verified Dalamud package, immutable per version.
- Adjacent `.zip.sha256` and `.json`: checksum, clean source commit and build evidence.
- `images/SHA256.png`: the exact packaged installer icon at a content-hashed URL.

The product README is preserved by publication. Packages contain the plugin, its
contracts/core libraries, required runtime dependency, icon and license notices;
Dalamud host libraries are excluded.

## Publishing

1. Bump the plugin's four-part numeric version for changed package bytes.
2. Push source to `main`. Wait for successful **Plugin package** and
   **Server and contracts** checks for that exact source commit.
3. Run **Plugin package** manually from `main` with `publish=true`. It downloads
   its verified Windows artifact and checks the source still matches `main`.
4. The publishing job calls `scripts/prepare-plugin-repository.py` and commits
   the feed, versioned package, checksum, evidence and icon to `main` together.

Publication refuses changed bytes at an existing version or a version downgrade.
Distribution-only commits are excluded from build triggers to avoid recursion.
CI commits use the GitHub Actions bot identity. Local commits use the approved
Runmih identity; never import prior repository history into this clean repository.

## Stable and testing channels

`main` currently publishes the approved baseline. Future testing builds can be
maintained on a `testing` source branch and advertised through Dalamud's native
testing fields in this same feed. No testing candidate is advertised by 0.0.6.15.

The feed is publicly downloadable; adding a custom feed does not constitute
official Dalamud repository acceptance or approval of the final 1.0 release.
