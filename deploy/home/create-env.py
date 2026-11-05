#!/usr/bin/env python3
"""Create fresh-install secrets without printing them or overwriting an existing file."""
import argparse
import base64
import ipaddress
import os
from pathlib import Path
import re
import secrets
from urllib.parse import urlsplit


def origin(value):
    parsed = urlsplit(value)
    if (parsed.scheme != "https" or not parsed.hostname or parsed.username is not None
            or parsed.password is not None or parsed.query or parsed.fragment
            or parsed.path not in ("", "/") or not re.fullmatch(r"[A-Za-z0-9.-]+", parsed.hostname)):
        raise argparse.ArgumentTypeError("Use an HTTPS origin without credentials, paths, query or fragment.")
    try:
        parsed.port
    except ValueError as error:
        raise argparse.ArgumentTypeError("Invalid HTTPS port.") from error
    return value.rstrip("/")


def proxy(value):
    try:
        address = ipaddress.ip_address(value)
    except ValueError as error:
        raise argparse.ArgumentTypeError("Use the exact pinned proxy container IP, without /prefix.") from error
    if address.is_unspecified or address.is_multicast or address.is_loopback:
        raise argparse.ArgumentTypeError("Use the proxy container address, not a wildcard or loopback address.")
    return str(address)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--origin", required=True, type=origin)
    parser.add_argument("--proxy-ip", required=True, type=proxy)
    parser.add_argument("--image", default="amorrp-server:0.3.0.0")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if not re.fullmatch(r"amorrp-server:[A-Za-z0-9_.-]+", args.image):
        parser.error("Use the local amorrp-server image tag built from this repository.")
    values = {
        "AMORRP_SERVER_IMAGE": args.image,
        "AMORRP_PUBLIC_ORIGIN": args.origin,
        "AMORRP_PROXY_IP": args.proxy_ip,
        "AMORRP_DB_PASSWORD": secrets.token_hex(32),
        "AMORRP_VAULT_KEY": base64.b64encode(secrets.token_bytes(32)).decode("ascii"),
        "XIVAUTH_CLIENT_ID": "",
        "XIVAUTH_CLIENT_SECRET": "",
    }
    # O_EXCL refuses existing files (including symlinks); rerunning cannot silently
    # replace the vault key or change a password on an already initialized volume.
    try:
        descriptor = os.open(args.output, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except FileExistsError:
        parser.exit(1, "Refusing to overwrite existing secrets. Reuse the original file for updates.\n")
    with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
        stream.write("# Private Amor RP configuration. Keep this file and the vault key with your backups.\n")
        stream.write("# Enter XIVAuth credentials in Portainer before deployment; never share them in chat.\n")
        stream.writelines(f"{key}={value}\n" for key, value in values.items())
    print(f"Created private configuration: {args.output}")
    print("Transfer it privately to Portainer and fill in XIVAUTH_CLIENT_ID and XIVAUTH_CLIENT_SECRET there.")
    print("The same database password and vault key must be retained on every update.")


if __name__ == "__main__":
    main()
