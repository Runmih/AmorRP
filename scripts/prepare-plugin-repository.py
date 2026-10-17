"""Prepare the shared Dalamud feed with independent stable and testing channels.

Uses a checked inner latest.zip. Publishing is a separate Git push. Versioned
packages and their provenance are immutable across both channels.
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
TEST_FIELDS = ("TestingAssemblyVersion", "TestingDalamudApiLevel", "DownloadLinkTesting")

def number(version):
    if not re.fullmatch(r"\d+\.\d+\.\d+\.\d+", version):
        raise ValueError("Expected a four-part numeric assembly version")
    return tuple(map(int, version.split(".")))

def prepare(package, destination, source_commit, build_url, channel="stable"):
    if channel not in ("stable", "testing"):
        raise ValueError("Unknown release channel")
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
    number(version)
    feed_path = destination / "repo.json"
    previous = None
    if feed_path.exists():
        previous, = json.loads(feed_path.read_text(encoding="utf-8"))
        if previous["InternalName"] != "AmorRP":
            raise ValueError("Unexpected plugin in existing feed")
        if number(previous["AssemblyVersion"]) > number(version):
            raise ValueError("Refusing to downgrade the stable feed")
    target = destination / "packages" / f"AmorRP-{version}.zip"
    exists = target.exists()
    if exists and target.read_bytes() != data:
        raise ValueError("This version already has different bytes; bump the plugin version")
    if exists and previous is None:
        raise ValueError("Package exists without a feed; inspect the publication manually")
    download = f"{BASE}/packages/{target.name}"
    if channel == "testing":
        if previous is None or number(version) <= number(previous["AssemblyVersion"]):
            raise ValueError("A testing candidate must be newer than an existing stable version")
        stable_zip = destination / "packages" / f"AmorRP-{previous['AssemblyVersion']}.zip"
        if not stable_zip.is_file():
            raise ValueError("Stable package must remain available when adding a testing candidate")
        if previous.get("TestingAssemblyVersion") and number(previous["TestingAssemblyVersion"]) > number(version):
            raise ValueError("Refusing to downgrade the testing candidate")
        feed = dict(previous)
        feed.update(TestingAssemblyVersion=version, TestingDalamudApiLevel=manifest["DalamudApiLevel"], DownloadLinkTesting=download)
    else:
        feed = dict(manifest)
        feed.update(DownloadLinkInstall=download, DownloadLinkUpdate=download, DownloadLinkTesting=download, IsHide=False, IsTestingExclusive=False)
        if previous and previous.get("TestingAssemblyVersion") and number(previous["TestingAssemblyVersion"]) > number(version):
            for key in TEST_FIELDS:
                feed[key] = previous[key]
    # Retrying the same verified publication must not alter timestamp or evidence.
    if exists and previous and all(previous.get(k) == v for k, v in feed.items() if k != "LastUpdate") and set(previous) - {"LastUpdate"} == set(feed) - {"LastUpdate"}:
        print(f"Already published: {channel} {version} ({digest}); no files changed")
        return
    feed["LastUpdate"] = int(time.time())
    destination.mkdir(parents=True, exist_ok=True)
    target.parent.mkdir(exist_ok=True)
    image_folder = destination / "images"
    image_folder.mkdir(exist_ok=True)
    (image_folder / f"{icon_hash}.png").write_bytes(icon)
    if not exists:
        target.write_bytes(data)
        target.with_suffix(".zip.sha256").write_text(f"{digest}  {target.name}\n", encoding="utf-8")
        evidence = dict(version=version, sha256=digest, sourceCommit=source_commit, buildUrl=build_url, channel=channel)
        target.with_suffix(".json").write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
    feed_path.write_text(json.dumps([feed], indent=2) + "\n", encoding="utf-8")
    (destination / "LICENSE.md").write_bytes((ROOT / "LICENSE.md").read_bytes())
    if destination.resolve() != ROOT.resolve() and not (destination / "README.md").exists():
        (destination / "README.md").write_bytes((ROOT / "README.md").read_bytes())
    print(f"Prepared {channel} {version}; SHA-256 {digest}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", required=True, type=Path)
    parser.add_argument("--destination", required=True, type=Path)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--build-url", required=True)
    parser.add_argument("--channel", choices=["stable", "testing"], default="stable")
    args = parser.parse_args()
    prepare(args.package, args.destination, args.source_commit, args.build_url, args.channel)
