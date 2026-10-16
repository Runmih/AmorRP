"""Prepare the main-branch Dalamud feed; publishing it is a separate Git push.

Uses the checked inner latest.zip, never an Actions artifact wrapper. Existing
versioned packages are immutable and the advertised version cannot go backwards.
"""

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import time
import zipfile

ROOT = Path(__file__).resolve().parents[1]
BASE = "https://raw.githubusercontent.com/Runmih/AmorRP/main"


def prepare(package, destination, source_commit, build_url):
    if not re.fullmatch(r"[0-9a-f]{40}", source_commit):
        raise ValueError("Source commit must be the full Git SHA")
    if not build_url.startswith("https://github.com/Runmih/AmorRP/"):
        raise ValueError("Build/source evidence must belong to this repository")
    subprocess.run([sys.executable, str(ROOT / "scripts/check-plugin-package.py"), str(package)], check=True)
    data = package.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    with zipfile.ZipFile(package) as archive:
        manifest = json.loads(archive.read("AmorRP.json"))
        icon = archive.read("icon.png")
    icon_hash = hashlib.sha256(icon).hexdigest()
    version = manifest["AssemblyVersion"]
    if not re.fullmatch(r"\d+\.\d+\.\d+\.\d+", version):
        raise ValueError("Expected a four-part numeric assembly version")
    destination.mkdir(parents=True, exist_ok=True)
    feed_path = destination / "repo.json"
    if feed_path.exists():
        previous, = json.loads(feed_path.read_text(encoding="utf-8"))
        if previous["InternalName"] != "AmorRP":
            raise ValueError("Unexpected plugin in existing feed")
        if tuple(map(int, previous["AssemblyVersion"].split("."))) > tuple(map(int, version.split("."))):
            raise ValueError("Refusing to downgrade the published feed; issue a new patch version")
    target = destination / "packages" / f"AmorRP-{version}.zip"
    if target.exists():
        if target.read_bytes() != data:
            raise ValueError("This version already has different bytes; bump the plugin version")
        if not feed_path.exists():
            raise ValueError("Package exists without a feed; inspect the publication manually")
        print(f"Already published: {version} ({digest}); no files changed")
        return
    # Validate everything before writing any public file. ZIP, feed and evidence
    # are committed together so an incomplete upload cannot become an update.
    manifest.update({
        "DownloadLinkInstall": f"{BASE}/packages/AmorRP-{version}.zip",
        "DownloadLinkUpdate": f"{BASE}/packages/AmorRP-{version}.zip",
        "DownloadLinkTesting": f"{BASE}/packages/AmorRP-{version}.zip",
        "LastUpdate": int(time.time()),
        "IsHide": False,
        "IsTestingExclusive": False,
    })
    target.parent.mkdir(exist_ok=True)
    image_folder = destination / "images"
    image_folder.mkdir(exist_ok=True)
    (image_folder / f"{icon_hash}.png").write_bytes(icon)
    target.write_bytes(data)
    (target.with_suffix(".zip.sha256")).write_text(f"{digest}  {target.name}\n", encoding="utf-8")
    evidence = {"version": version, "sha256": digest, "sourceCommit": source_commit, "buildUrl": build_url}
    target.with_suffix(".json").write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
    feed_path.write_text(json.dumps([manifest], indent=2) + "\n", encoding="utf-8")
    (destination / "LICENSE.md").write_bytes((ROOT / "LICENSE.md").read_bytes())
    # Preserve the product README when publishing into the source branch.
    # A standalone staging directory receives the same reviewed introduction.
    if destination.resolve() != ROOT.resolve():
        (destination / "README.md").write_bytes((ROOT / "README.md").read_bytes())
    print(f"Prepared {version}; SHA-256 {digest}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", required=True, type=Path)
    parser.add_argument("--destination", required=True, type=Path)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--build-url", required=True)
    args = parser.parse_args()
    prepare(args.package, args.destination, args.source_commit, args.build_url)
