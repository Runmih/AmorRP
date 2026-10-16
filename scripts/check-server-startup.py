"""Exercise the real container wrapper with a fake dotnet process, without a DB."""

import json
import os
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix="amorrp-startup-") as directory:
    temporary = Path(directory)
    dotnet = temporary / "dotnet"
    dotnet.write_text(
        "#!/usr/bin/env python3\n"
        "import json, os, sys\n"
        "with open(os.environ['AMORRP_STARTUP_TEST_LOG'], 'a') as stream:\n"
        "    stream.write(json.dumps({'args': sys.argv[1:], 'pid': os.getpid()}) + '\\n')\n"
        "if '--migrate' in sys.argv:\n"
        "    sys.exit(int(os.environ.get('AMORRP_STARTUP_TEST_FAILURE', '0')))\n",
        encoding="utf-8",
    )
    dotnet.chmod(0o755)

    def run(flag=None, args=(), failure=0):
        log = temporary / "calls.jsonl"
        log.unlink(missing_ok=True)
        environment = dict(os.environ)
        environment.pop("AMORRP_MIGRATE_ON_START", None)
        if flag is not None:
            environment["AMORRP_MIGRATE_ON_START"] = flag
        environment["PATH"] = str(temporary) + os.pathsep + environment["PATH"]
        environment["AMORRP_STARTUP_TEST_LOG"] = str(log)
        environment["AMORRP_STARTUP_TEST_FAILURE"] = str(failure)
        process = subprocess.Popen(
            ["/bin/sh", str(root / "deploy/start-server.sh"), *args],
            env=environment, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        )
        _, stderr = process.communicate(timeout=10)
        calls = [json.loads(line) for line in log.read_text().splitlines()] if log.exists() else []
        return process.returncode, process.pid, calls, stderr.decode()

    for flag in (None, "false"):
        status, pid, calls, _ = run(flag)
        assert status == 0 and len(calls) == 1
        assert calls[0] == {"args": ["AmorRP.Server.dll"], "pid": pid}

    status, pid, calls, _ = run("true")
    assert status == 0 and [call["args"] for call in calls] == [
        ["AmorRP.Server.dll", "--migrate"], ["AmorRP.Server.dll"],
    ]
    assert calls[-1]["pid"] == pid, "Server must replace the shell for termination signals."

    status, _, calls, _ = run("true", failure=23)
    assert status == 23 and [call["args"] for call in calls] == [["AmorRP.Server.dll", "--migrate"]]

    status, _, calls, _ = run("true", args=["--migrate"])
    assert status == 0 and [call["args"] for call in calls] == [["AmorRP.Server.dll", "--migrate"]]

    status, pid, calls, _ = run("true", args=["--urls", "http://0.0.0.0:8080"])
    assert status == 0 and len(calls) == 2
    assert calls[-1] == {"args": ["AmorRP.Server.dll", "--urls", "http://0.0.0.0:8080"], "pid": pid}

    status, _, calls, error = run("invalid")
    assert status == 64 and not calls and "must be true or false" in error

print("PASS: 7 startup scenarios; migration failure blocks startup, explicit migration and exec preserved.")
