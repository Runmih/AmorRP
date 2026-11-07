#!/usr/bin/env python3
"""Build verified main for a manual Portainer update; never redeploy or read secrets."""
import json
from pathlib import Path
import re
import subprocess
import sys
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
REPOSITORY = "https://github.com/Runmih/AmorRP.git"


def run(*args, capture=False):
    result = subprocess.run(args, cwd=ROOT, check=True, text=True,
                            stdout=subprocess.PIPE if capture else None)
    return result.stdout.strip() if capture else None


def require_verified(commit):
    url = ("https://api.github.com/repos/Runmih/AmorRP/actions/workflows/"
           f"server-ci.yml/runs?head_sha={commit}&branch=main&per_page=20")
    request = urllib.request.Request(url, headers={
        "Accept": "application/vnd.github+json", "User-Agent": "AmorRP-home-updater"})
    with urllib.request.urlopen(request, timeout=30) as response:
        runs = json.load(response)["workflow_runs"]
    eligible = [r for r in runs if r["head_sha"] == commit
                and r["head_branch"] == "main"
                and r["event"] in ("push", "workflow_dispatch")]
    latest = max(eligible, key=lambda r: (r["run_number"], r["run_attempt"]), default=None)
    if not latest or latest["status"] != "completed" or latest["conclusion"] != "success":
        raise ValueError("The latest main commit has not passed Server and contracts. "
                         "Wait for its GitHub checks to finish successfully, then try again.")


def main():
    if run("git", "branch", "--show-current", capture=True) != "main":
        raise ValueError("Use the main checkout for server updates.")
    if run("git", "status", "--porcelain", capture=True):
        raise ValueError("The source folder has local changes. Resolve them before updating; "
                         "the updater will not discard files.")
    if run("git", "remote", "get-url", "origin", capture=True) != REPOSITORY:
        raise ValueError("Expected the Runmih/AmorRP HTTPS origin.")
    run("git", "fetch", "origin", "main")
    commit = run("git", "rev-parse", "origin/main", capture=True)
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("Git returned an invalid source commit.")
    # Also reject local-only commits instead of building a different source tree.
    run("git", "merge-base", "--is-ancestor", "HEAD", commit)
    print("Checking GitHub's server verification...", flush=True)
    require_verified(commit)
    run("git", "merge", "--ff-only", commit)
    version = ET.parse(ROOT / "src/AmorRP.Server/AmorRP.Server.csproj").findtext(".//Version")
    if not version or not re.fullmatch(r"\d+\.\d+\.\d+\.\d+", version):
        raise ValueError("The server project has no valid four-part version.")
    image = f"amorrp-server:{version}-{commit[:12]}"
    print(f"Building {image}. The running service stays in place.", flush=True)
    run("sudo", "docker", "build", "--file", "deploy/Dockerfile",
        "--label", f"org.opencontainers.image.revision={commit}",
        "--label", f"org.opencontainers.image.version={version}", "--tag", image, ".")
    image_id = run("sudo", "docker", "image", "inspect", image,
                   "--format", "{{.Id}}", capture=True)
    print(f"\nBUILD READY\nSource: {commit}\nImage ID: {image_id}\n")
    print("In Portainer: Stacks > amorrp > Editor > Environment variables")
    print(f"Set AMORRP_SERVER_IMAGE to: {image}")
    print("Update the stack with image re-pull disabled. Keep all other settings and volumes.")
    print("Then open your public /health/ready and /api/v2/capabilities endpoints.")
    print("No deployment, backup, database change or private-settings change was performed by this script.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, subprocess.CalledProcessError, urllib.error.URLError,
            OSError, KeyError, ET.ParseError) as error:
        print(f"UPDATE NOT PREPARED: {error}", file=sys.stderr)
        print("The running containers have not been changed.", file=sys.stderr)
        sys.exit(1)
