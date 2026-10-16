"""Validate project documentation and API design; does not test the application."""

from pathlib import Path
import re
import sys
from urllib.parse import unquote

import yaml
from openapi_spec_validator import validate


ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
METHODS = {"get", "post", "put", "patch", "delete", "head", "options"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def check_refs(value, spec):
    if isinstance(value, dict):
        if "$ref" in value:
            pointer = value["$ref"]
            require(pointer.startswith("#/"), f"Unexpected remote reference: {pointer}")
            target = spec
            for part in pointer[2:].split("/"):
                target = target[part.replace("~1", "/").replace("~0", "~")]
        for nested in value.values():
            check_refs(nested, spec)
    elif isinstance(value, list):
        for nested in value:
            check_refs(nested, spec)


def main():
    files = [ROOT / "README.md", ROOT / "CONTRIBUTING.md", *sorted(DOCS.rglob("*.md"))]
    links = 0
    for file in files:
        text = re.sub(r"```.*?```", "", file.read_text(encoding="utf-8"), flags=re.S)
        for destination in re.findall(r"\[[^\]]+\]\(([^)]+)\)", text):
            destination = destination.strip("<>").split("#", 1)[0]
            if not destination or re.match(r"^[a-z][a-z0-9+.-]*:", destination, re.I):
                continue
            target = (file.parent / unquote(destination)).resolve()
            require(target.is_relative_to(ROOT), f"Link escapes repository: {file}: {destination}")
            require(target.is_file(), f"Broken local link: {file}: {destination}")
            links += 1

    spec = yaml.safe_load((DOCS / "engineering/openapi.yaml").read_text(encoding="utf-8"))
    validate(spec)
    check_refs(spec, spec)
    operations, ids, implemented = set(), set(), set()
    for path, item in spec["paths"].items():
        for method, operation in item.items():
            if method not in METHODS:
                continue
            operations.add((method.upper(), path))
            operation_id = operation["operationId"]
            require(operation_id not in ids, f"Duplicate operationId: {operation_id}")
            ids.add(operation_id)
            require(operation.get("x-authorization-policy"), f"Missing policy: {operation_id}")
            require(operation.get("x-implementation-status") in {"planned", "implemented"}, f"Wrong status: {operation_id}")
            if operation["x-implementation-status"] == "implemented":
                implemented.add((method.upper(), path))
            params = operation.get("parameters", [])
            actual = {p["name"] for p in params if p["in"] == "path"}
            require(actual == set(re.findall(r"\{([^}]+)\}", path)), f"Path parameters: {operation_id}")
            if method not in {"get", "head", "options"}:
                require(any(p["name"] == "Idempotency-Key" and p["required"] for p in params),
                        f"Missing operation key: {operation_id}")

    guide = (DOCS / "engineering/api.md").read_text(encoding="utf-8")
    rows = re.findall(r"^\| (GET|POST|PUT|PATCH|DELETE)(?: \*)? \| `([^`]+)`", guide, re.M)
    require(len(rows) == len(set(rows)), "Duplicate endpoint catalog row")
    require(set(rows) == operations, "Endpoint catalog does not match OpenAPI")
    source_routes = set()
    for file in (ROOT / "src/AmorRP.Server/Features").rglob("*.cs"):
        for method, path in re.findall(r'app\.Map(Get|Post|Put|Patch|Delete)\("([^"]+)"', file.read_text()):
            source_routes.add((method.upper(), path))
    require(source_routes == implemented, "Implemented source routes do not match OpenAPI status")
    for name, schema in spec["components"]["schemas"].items():
        require(set(schema.get("required", [])) <= set(schema.get("properties", {})),
                f"Undefined required property: {name}")

    scope = (DOCS / "product/scope.md").read_text(encoding="utf-8")
    acceptance = (DOCS / "delivery/acceptance.md").read_text(encoding="utf-8")
    features = set(re.findall(r"\bF\d{2}\b", scope))
    require(features == set(re.findall(r"\bF\d{2}\b", acceptance)), "Feature/acceptance coverage differs")
    print(f"PASS: {len(files)} Markdown files, {links} local links, {len(operations)} operations, "
          f"{len(spec['components']['schemas'])} schemas, {len(features)} features covered. "
          "Application behavior is not tested.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        sys.exit(1)
