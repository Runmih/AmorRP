# Dalamud repository and release channels

The single installation URL is:

```text
https://raw.githubusercontent.com/Runmih/AmorRP/main/repo.json
```

`main` is stable source. `testing` contains opt-in candidates. Dalamud chooses the
channel from repo.json, not the Git branch name. Stable uses AssemblyVersion,
DownloadLinkInstall and DownloadLinkUpdate. Testing additionally uses
TestingAssemblyVersion, TestingDalamudApiLevel and DownloadLinkTesting.

Build and validate a Windows package from the selected source commit. Both Plugin
package and Server and contracts workflows must succeed for that exact commit.
Use `scripts/prepare-plugin-repository.py --channel testing` with the checked inner
latest.zip and a destination checked out on main. Commit only distribution files
there. Stable bytes, version, description and install/update links remain unchanged.
The publisher rejects reused versions with different bytes and channel downgrades.

Both workflows build pushes to main and testing. Manual publishing from either
branch chooses the corresponding channel, verifies that its source branch has not
changed, and writes the shared feed on main. The source branch is not merged to
main by publication. Stable promotion removes a testing candidate at or below the
new stable version and retains any newer candidate.

See [0.0.6.17 testing](release-0.0.6.17-testing.md) for deployment and opt-in details.
