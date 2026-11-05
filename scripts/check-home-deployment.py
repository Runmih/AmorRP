"""Check home-stack isolation, required settings and safe secret generation; no services start."""
import base64
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parents[1]
script = root / "deploy/home/create-env.py"
compose = root / "deploy/home/compose.yaml"
with tempfile.TemporaryDirectory(prefix="amorrp-home-check-") as directory:
    destination = Path(directory) / "private.env"
    args = [sys.executable, str(script), "--origin", "https://api.example.invalid/", "--proxy-ip", "172.30.0.3", "--output", str(destination)]
    generated = subprocess.run(args, capture_output=True, text=True, check=True)
    values = dict(line.split("=", 1) for line in destination.read_text().splitlines() if line and not line.startswith("#"))
    assert len(bytes.fromhex(values["AMORRP_DB_PASSWORD"])) == 32
    assert len(base64.b64decode(values["AMORRP_VAULT_KEY"], validate=True)) == 32
    assert values["AMORRP_PUBLIC_ORIGIN"] == "https://api.example.invalid"
    assert stat.S_IMODE(destination.stat().st_mode) == 0o600
    assert values["AMORRP_DB_PASSWORD"] not in generated.stdout + generated.stderr
    assert values["AMORRP_VAULT_KEY"] not in generated.stdout + generated.stderr
    original = destination.read_bytes()
    assert subprocess.run(args, capture_output=True).returncode != 0
    assert destination.read_bytes() == original, "An existing installation's keys must never be overwritten"
    environment = dict(os.environ)
    # Ignore any unrelated inherited installation configuration; these are disposable check values.
    for name in values:
        environment.pop(name, None)
    command = ["docker", "compose", "--env-file", str(destination), "--file", str(compose), "config", "--format", "json"]
    missing = subprocess.run(command, env=environment, capture_output=True)
    assert missing.returncode != 0, "An unconfigured identity provider must block deployment"
    environment.update(XIVAUTH_CLIENT_ID="deployment-check", XIVAUTH_CLIENT_SECRET="deployment-check-only")
    config = json.loads(subprocess.run(command, env=environment, capture_output=True, text=True, check=True).stdout)
    db, api = config["services"]["database"], config["services"]["api"]
    assert not db.get("ports") and not api.get("ports"), "Production database/API must not publish host ports"
    assert set(db["networks"]) == {"database"} and config["networks"]["database"]["internal"]
    assert set(api["networks"]) == {"database", "web-proxy"}
    assert config["networks"]["web-proxy"]["external"] and config["networks"]["web-proxy"]["name"] == "web-proxy"
    assert "amorrp-api" in api["networks"]["web-proxy"]["aliases"]
    setting = api["environment"]
    assert setting["ReverseProxy__UseRenderClientIp"] == "false"
    assert setting["ReverseProxy__KnownProxies__0"] == "172.30.0.3" and setting["ReverseProxy__ForwardLimit"] == "1"
    assert not any("KnownNetworks" in key for key in setting)
    assert setting["AMORRP_MIGRATE_ON_START"] == "true" and setting["XivAuth__DurableEnabled"] == "true"
    assert setting["XivAuth__DurableCallbackUrl"] == "https://api.example.invalid/auth/xivauth/callback"
    assert values["AMORRP_DB_PASSWORD"] in setting["ConnectionStrings__Database"]
    assert setting["SecretVault__Keys__primary"] == values["AMORRP_VAULT_KEY"]
    assert api["pull_policy"] == "never" and api["restart"] == db["restart"] == "unless-stopped"
    for service in (api, db):
        assert service["logging"]["options"] == {"max-size": "10m", "max-file": "3"}
    assert api["depends_on"]["database"]["condition"] == "service_healthy"
    assert db["volumes"][0]["target"] == "/var/lib/postgresql"
    # Bad origins/proxy addresses must fail without creating a secret file.
    for flag, bad in [("--origin", "http://api.example.invalid"), ("--origin", "https://user:password@api.example.invalid"),
                      ("--origin", "https://api.example.invalid/api/v2"), ("--proxy-ip", "0.0.0.0"), ("--proxy-ip", "172.30.0.0/16")]:
        invalid = list(args); invalid[invalid.index(flag) + 1] = bad
        invalid[-1] = str(Path(directory) / "must-not-exist.env")
        assert subprocess.run(invalid, capture_output=True).returncode != 0
        assert not Path(invalid[-1]).exists()
print("PASS: private/reusable secrets, required credentials, isolated persistent database, proxy-only API and bounded logs.")
