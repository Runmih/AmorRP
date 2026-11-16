# Dalamud installer feed

Use the existing single feed:

```text
https://raw.githubusercontent.com/Runmih/AmorRP/main/repo.json
```

Source and distribution use main. The publisher prepares a stable manifest,
immutable versioned ZIP, SHA-256 sidecar and source/build provenance. Installer,
update and testing-download links point to that same published package; there is
no separate testing candidate workflow. Old immutable packages remain available.

Both exact-source Windows and server/contracts workflows must pass before
publication. Download the Windows inner latest.zip, verify its artifact/package
digests and run prepare-plugin-repository.py with package, destination, source-commit
and build-url. Validate the public feed and downloaded package bytes afterwards.
Rejected downgrade/reused-version/source evidence must leave published files intact;
retrying the same publication must preserve timestamp and provenance.

See [development](development.md), [versions](../engineering/versions.md) and
[release history](history.md). CI validates packages; game loading/rendering still
requires the acceptance checklist. Official repository submission and 1.0 approval
are separate future gates.
