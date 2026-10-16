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
