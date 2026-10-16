"""Bundle verified build outputs and the current source for local milestone testing."""

import argparse
import hashlib
import os
import shutil
import subprocess
import sys
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

parser = argparse.ArgumentParser(description="Package plugin, published server and source for a milestone.")
parser.add_argument("--milestone", required=True)
milestone = parser.parse_args().milestone
if not milestone.isascii() or not milestone.isalnum() or len(milestone) > 32:
    raise SystemExit("Milestone must be a short ASCII alphanumeric label.")

root = Path(__file__).resolve().parents[1]
configured = ET.parse(root / "Directory.Build.props").findtext(".//Milestone")
if configured and configured != milestone:
    raise SystemExit(f"This checkout is {configured}; use its milestone or check out the historical branch.")
artifacts = root / "artifacts"
plugin = root / "src/AmorRP.Plugin/bin/x64/Release/AmorRP/latest.zip"
server = artifacts / "server"
if not (server / "AmorRP.Server.dll").is_file():
    raise SystemExit("Publish the server to artifacts/server before packaging.")
subprocess.run([sys.executable, str(root / "scripts/check-plugin-package.py"), str(plugin)], check=True)
artifacts.mkdir(exist_ok=True)
shutil.copyfile(plugin, artifacts / f"AmorRP-{milestone}-plugin.zip")
with zipfile.ZipFile(artifacts / f"AmorRP-{milestone}-server.zip", "w", zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(server.rglob("*")):
        if path.is_file():
            archive.write(path, path.relative_to(server))
    if not (server / "LICENSE.md").is_file():
        archive.write(root / "LICENSE.md", "LICENSE.md")
with zipfile.ZipFile(artifacts / f"AmorRP-{milestone}-source.zip", "w", zipfile.ZIP_DEFLATED) as archive:
    excluded = {".git", ".tools", ".vs", ".idea", "node_modules", "bin", "obj", "artifacts", "__pycache__", "TestResults", ".venv"}
    for directory, folders, files in os.walk(root):
        folders[:] = sorted(folder for folder in folders if folder not in excluded)
        for name in sorted(files):
            if (name == ".env" or name.startswith(".env.") and name != ".env.example"
                    or name.endswith((".user", ".suo", ".local.json", ".db"))):
                continue
            path = Path(directory) / name
            if path.is_file() and not path.is_symlink():
                archive.write(path, path.relative_to(root))
checksums = []
for path in sorted(artifacts.glob(f"AmorRP-{milestone}-*.zip")):
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    checksums.append(f"{digest}  {path.name}")
    print(f"Packaged {path.name} ({path.stat().st_size:,} bytes)")
(artifacts / f"SHA256SUMS-{milestone}.txt").write_text("\n".join(checksums) + "\n", encoding="utf-8")
