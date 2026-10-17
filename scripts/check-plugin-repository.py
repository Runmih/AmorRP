"""Exercise publication guards against the actual built ZIP, without publishing."""

import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import zipfile

spec = importlib.util.spec_from_file_location("publisher", Path(__file__).with_name("prepare-plugin-repository.py"))
publisher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(publisher)
package = publisher.ROOT / "src/AmorRP.Plugin/bin/x64/Release/AmorRP/latest.zip"


def snapshot(destination):
    return {str(p.relative_to(destination)): p.read_bytes() for p in destination.rglob("*") if p.is_file()}


with tempfile.TemporaryDirectory() as temporary:
    destination = Path(temporary) / "feed"

    def prepare(path=package, commit="a" * 40):
        publisher.prepare(path, destination, commit, "https://github.com/Runmih/AmorRP/actions/runs/1")

    prepare()
    manifest, = json.loads((destination / "repo.json").read_text())
    expected = f"{publisher.BASE}/packages/AmorRP-{manifest['AssemblyVersion']}.zip"
    assert all(manifest[key] == expected for key in ("DownloadLinkInstall", "DownloadLinkUpdate", "DownloadLinkTesting"))
    assert (destination / "packages" / expected.rsplit("/", 1)[1]).read_bytes() == package.read_bytes()
    with zipfile.ZipFile(package) as archive:
        assert (destination / "images" / manifest["IconUrl"].rsplit("/", 1)[1]).read_bytes() == archive.read("icon.png")
    assert (destination / "README.md").read_bytes() == (publisher.ROOT / "README.md").read_bytes()
    original = snapshot(destination)
    from unittest.mock import patch
    with patch.object(publisher.time, "time", return_value=publisher.time.time() + 60):
        prepare()  # Retry preserves exact feed timestamp, provenance and bytes.
    assert snapshot(destination) == original

    def rejects(action):
        before = snapshot(destination)
        try:
            with contextlib.redirect_stdout(io.StringIO()):
                action()
        except ValueError:
            pass
        else:
            raise AssertionError("Unsafe publication was accepted")
        assert snapshot(destination) == before, "Rejected publication changed public files"

    changed = Path(temporary) / "changed.zip"
    changed.write_bytes(package.read_bytes())
    with zipfile.ZipFile(changed, "a") as archive:
        archive.writestr("changed.txt", "Different package at the same version")
    rejects(lambda: prepare(changed))
    rejects(lambda: prepare(commit="invalid"))
    manifest["AssemblyVersion"] = "99.0.0.0"
    (destination / "repo.json").write_text(json.dumps([manifest]))
    rejects(prepare)

print("PASS: real ZIP feed links/bytes, idempotent retry, immutable versions, source validation, downgrade rejection; failures preserve public files")

# Use the actual shipped stable ZIP and manifest. Testing must not replace a
# stable install or make the experimental build a compulsory upgrade.
with tempfile.TemporaryDirectory() as temporary:
    import shutil
    destination = Path(temporary) / "shared"
    destination.mkdir()
    stable, = json.loads((publisher.ROOT / "repo.json").read_text())
    stable.pop("TestingAssemblyVersion", None)
    stable.pop("TestingDalamudApiLevel", None)
    stable["DownloadLinkTesting"] = stable["DownloadLinkInstall"]
    stable_package = f"AmorRP-{stable['AssemblyVersion']}.zip"
    (destination / "repo.json").write_text(json.dumps([stable]))
    (destination / "packages").mkdir()
    shutil.copyfile(publisher.ROOT / "packages" / stable_package, destination / "packages" / stable_package)
    # This test applies when checking a newer candidate on testing, and remains
    # meaningful for main's same-version maintenance builds without assuming a candidate.
    with zipfile.ZipFile(package) as archive:
        candidate = json.loads(archive.read("AmorRP.json"))
    if publisher.number(candidate["AssemblyVersion"]) > publisher.number(stable["AssemblyVersion"]):
        original_stable_bytes = (destination / "packages" / stable_package).read_bytes()
        publisher.prepare(package, destination, "a" * 40, "https://github.com/Runmih/AmorRP/actions/runs/1", "testing")
        testing, = json.loads((destination / "repo.json").read_text())
        assert all(testing[k] == v for k, v in stable.items() if k not in publisher.TEST_FIELDS and k != "LastUpdate")
        assert testing["TestingAssemblyVersion"] == candidate["AssemblyVersion"]
        assert testing["TestingDalamudApiLevel"] == candidate["DalamudApiLevel"]
        assert testing["DownloadLinkTesting"].endswith(f"AmorRP-{candidate['AssemblyVersion']}.zip")
        assert (destination / "packages" / stable_package).read_bytes() == original_stable_bytes
        before = snapshot(destination)
        publisher.prepare(package, destination, "b" * 40, "https://github.com/Runmih/AmorRP/actions/runs/2", "testing")
        assert snapshot(destination) == before
        publisher.prepare(package, destination, "c" * 40, "https://github.com/Runmih/AmorRP/actions/runs/3", "stable")
        promoted, = json.loads((destination / "repo.json").read_text())
        assert promoted["AssemblyVersion"] == candidate["AssemblyVersion"] and "TestingAssemblyVersion" not in promoted
        assert (destination / "packages" / stable_package).read_bytes() == original_stable_bytes
        print("PASS: native testing fields, unchanged stable files/metadata, testing retry and stable promotion")
