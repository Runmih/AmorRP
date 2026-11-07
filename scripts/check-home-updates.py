"""Check update admission and failed-build isolation without Docker, GitHub or host changes."""
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("home_updater", root / "deploy/home/prepare-update.py")
updater = importlib.util.module_from_spec(spec)
spec.loader.exec_module(updater)
SHA = "a" * 40
VERSION = ET.parse(root / "src/AmorRP.Server/AmorRP.Server.csproj").findtext(".//Version")


class UpdateChecks(unittest.TestCase):
    def verify(self, runs):
        with patch.object(updater.urllib.request, "urlopen", return_value=io.BytesIO(json.dumps({"workflow_runs": runs}).encode())):
            updater.require_verified(SHA)

    def workflow(self):
        return dict(head_sha=SHA, head_branch="main", event="push", run_number=1,
                    run_attempt=1, status="completed", conclusion="success")

    def test_missing_wrong_commit_and_unfinished_checks_are_rejected(self):
        for changes in [None, {"head_sha": "b" * 40}, {"event": "pull_request"},
                        {"status": "in_progress", "conclusion": None}, {"conclusion": "failure"}]:
            data = self.workflow()
            if changes:
                data.update(changes)
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                self.verify([] if changes is None else [data])

    def test_failed_retry_overrides_older_success(self):
        retry = self.workflow()
        retry.update(run_attempt=2, conclusion="failure")
        with self.assertRaises(ValueError):
            self.verify([self.workflow(), retry])
        self.verify([self.workflow()])

    def simulate(self, *, dirty=False, diverged=False, build_fails=False, checks_fail=False):
        calls = []

        def command(*args, capture=False):
            calls.append(args)
            results = {
                ("git", "branch", "--show-current"): "main",
                ("git", "status", "--porcelain"): " M local.txt" if dirty else "",
                ("git", "remote", "get-url", "origin"): updater.REPOSITORY,
                ("git", "rev-parse", "origin/main"): SHA,
            }
            if (diverged and args[:2] == ("git", "merge-base")) or (build_fails and args[:3] == ("sudo", "docker", "build")):
                raise subprocess.CalledProcessError(1, args)
            return results.get(args, "sha256:test" if capture else None)

        with patch.object(updater, "run", side_effect=command), patch.object(updater, "require_verified", side_effect=ValueError("Checks failed") if checks_fail else None) as verified, patch("sys.stdout", new_callable=io.StringIO) as output:
            error = None
            try:
                updater.main()
            except (ValueError, subprocess.CalledProcessError) as failure:
                error = failure
            return calls, verified.call_args_list, output.getvalue(), error

    def test_local_edits_and_divergence_never_reach_build(self):
        for settings in ({"dirty": True}, {"diverged": True}):
            calls, _, _, error = self.simulate(**settings)
            self.assertIsNotNone(error)
            self.assertFalse(any(c[:2] == ("sudo", "docker") for c in calls))

    def test_build_failure_never_reports_a_ready_update(self):
        calls, _, output, error = self.simulate(build_fails=True)
        self.assertIsNotNone(error)
        self.assertNotIn("BUILD READY", output)
        self.assertFalse(any(c[:4] == ("sudo", "docker", "image", "inspect") for c in calls))

    def test_failed_checks_never_merge_or_build(self):
        calls, _, _, error = self.simulate(checks_fail=True)
        self.assertIsNotNone(error)
        self.assertFalse(any(c[:2] == ("git", "merge") or c[:2] == ("sudo", "docker") for c in calls))

    def test_success_only_prepares_explicit_image_and_preserves_host_services(self):
        calls, verified, output, error = self.simulate()
        self.assertIsNone(error)
        self.assertEqual(len(verified), 1)
        self.assertEqual(verified[0].args, (SHA,))
        build, = [c for c in calls if c[:3] == ("sudo", "docker", "build")]
        self.assertIn(f"org.opencontainers.image.revision={SHA}", build)
        self.assertIn(f"amorrp-server:{VERSION}-aaaaaaaaaaaa", build)
        self.assertIn("BUILD READY", output)
        self.assertIn("AMORRP_SERVER_IMAGE", output)
        docker = [c[2] for c in calls if c[:2] == ("sudo", "docker")]
        self.assertEqual(docker, ["build", "image"])


if __name__ == "__main__":
    unittest.main()
