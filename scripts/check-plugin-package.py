"""Check the actual package, including identity and host-library exclusions."""

import json
import sys
import hashlib
import struct
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(__file__).resolve().parents[1]
package = Path(sys.argv[1]) if len(sys.argv) > 1 else root / "src/AmorRP.Plugin/bin/x64/Release/AmorRP/latest.zip"
with zipfile.ZipFile(package) as archive:
    names = set(archive.namelist())
    assert len(names) == len(archive.namelist()), "Duplicate ZIP entry"
    required = {"AmorRP.dll", "AmorRP.Contracts.dll", "AmorRP.Core.dll", "AmorRP.json", "AmorRP.deps.json", "System.Security.Cryptography.ProtectedData.dll", "AmorRP.licenses.json", "icon.png"}
    assert required <= names, f"Missing package files: {required - names}"
    manifest = json.loads(archive.read("AmorRP.json"))
    assert manifest["InternalName"] == "AmorRP"
    assert manifest["DalamudApiLevel"] == 15
    expected_version = ET.parse(root / "src/AmorRP.Plugin/AmorRP.Plugin.csproj").findtext(".//Version")
    assert manifest["AssemblyVersion"] == expected_version
    assert manifest["RepoUrl"] == "https://github.com/Runmih/AmorRP"
    icon = archive.read("icon.png")
    assert icon[:8] == b"\x89PNG\r\n\x1a\n", "Plugin icon must be PNG"
    width, height = struct.unpack(">II", icon[16:24])
    assert 0 < width == height <= 512, "Dalamud icon must be square and at most 512x512"
    icon_hash = hashlib.sha256(icon).hexdigest()
    assert manifest["IconUrl"] == f"https://raw.githubusercontent.com/Runmih/AmorRP/main/images/{icon_hash}.png", "Icon URL does not match packaged icon"
    for name in names:
        assert Path(name).name == name, f"Unexpected nested or unsafe entry: {name}"
        if name.lower().endswith(".dll"):
            assert name in {"AmorRP.dll", "AmorRP.Contracts.dll", "AmorRP.Core.dll", "System.Security.Cryptography.ProtectedData.dll"}, f"Unexpected bundled DLL: {name}"
print(f"PASS: {package.name}; API 15, correct identity, contracts/core included, no host DLLs")
