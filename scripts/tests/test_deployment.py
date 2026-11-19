"""Exercise the real Bash state machine with fake Docker/network tools, never real services."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

from release_fixtures import ROOT, manifest, settings
from release import write, main as release_main
from production_metadata import initialize
from unittest.mock import patch


@unittest.skipIf(
    os.name == "nt",
    "Linux filesystem/flock/symlink contracts run in a temporary Linux container or CI",
)
class DeploymentTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.app = self.root / "app"
        self.old = self.app / "releases" / ("b" * 40 + "-1-1")
        self.new = self.app / "releases" / ("a" * 40 + "-2-1")
        self.bin = self.root / "bin"
        self.bin.mkdir()
        self.log = self.root / "calls.log"
        self.env = {
            **os.environ,
            "UGTM_DEPLOY_ROOT": str(self.app),
            "DEPLOY_TEST_LOG": str(self.log),
            "FAKE_NEW": str(self.new),
            "PATH": str(self.bin) + os.pathsep + os.environ["PATH"],
        }
        for directory in (self.old, self.new):
            (directory / "scripts").mkdir(parents=True)
            (directory / "docker").mkdir()
            for script in (
                "Deploy-Production.sh",
                "production_metadata.py",
                "release.py",
            ):
                shutil.copy(ROOT / "scripts" / script, directory / "scripts" / script)
            for file in ("docker-compose.prod.yml", "Caddyfile", "initialize-minio.sh"):
                shutil.copy(ROOT / "docker" / file, directory / "docker" / file)
            write(directory / "release.json", manifest())
            write(directory / "settings.json", settings())
            with patch("sys.argv", ["release.py", "seal", str(directory)]):
                release_main()
        initialize(self.old)
        write(self.old / "status.json", {"status": "deployed"})
        (self.app / "current").symlink_to(self.old)
        docker = """#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
args=sys.argv[1:]
with open(os.environ['DEPLOY_TEST_LOG'],'a') as f: f.write(' '.join(args)+'\\n')
new=os.environ['FAKE_NEW'] in ' '.join(args)
if '--entrypoint' in args and 'id' in args: print(10001)
if '--check-schema' in args and new and os.environ.get('FAIL_SCHEMA'): sys.exit(1)
if 'up' in args and new and os.environ.get('FAIL_UP'): sys.exit(1)
if 'up' in args and not new and os.environ.get('FAIL_ROLLBACK'): sys.exit(1)
if 'drain' in args and os.environ.get('FAIL_DRAIN'): sys.exit(1)
if 'ps' in args and args[0]=='compose': print('worker-id' if '-q' in args else '{}')
if args[0]=='inspect' and '{{.State.Running}}' in args: print('true')
if args[0]=='inspect' and '{{.Config.Hostname}}' in args: print('worker-host')
"""
        curl = """#!/usr/bin/env python3
import os, sys
from pathlib import Path
with open(os.environ['DEPLOY_TEST_LOG'],'a') as f: f.write('curl\\n')
marker=Path(os.environ['FAKE_NEW'])/'curl-failed-once'
if os.environ.get('FAIL_HEALTH') and not marker.exists():
    marker.touch()
    sys.exit(1)
"""
        for name, content in {
            "docker": docker,
            "curl": curl,
            "setfacl": "#!/bin/sh\nexit 0\n",
        }.items():
            path = self.bin / name
            path.write_text(content)
            path.chmod(0o755)

    def invoke(self, mode, **variables):
        return subprocess.run(
            ["bash", str(self.new / "scripts/Deploy-Production.sh"), mode],
            env={**self.env, **variables},
            capture_output=True,
            text=True,
            timeout=30,
        )

    def assert_status(self, expected):
        self.assertEqual(
            json.loads((self.new / "status.json").read_text())["status"], expected
        )

    def test_prepare_never_contacts_database_or_changes_services(self):
        result = self.invoke("prepare")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assert_status("prepared")
        self.assertEqual((self.app / "current").resolve(), self.old)
        calls = self.log.read_text()
        self.assertNotIn(" up ", calls)
        self.assertNotIn("--check-schema", calls)
        self.assertNotIn("scripts.check_schema", calls)

    def test_schema_failure_preserves_current_and_does_not_start_new_services(self):
        result = self.invoke("candidate", FAIL_SCHEMA="1")
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual((self.app / "current").resolve(), self.old)
        self.assertNotIn(" up ", self.log.read_text())
        self.assertFalse((self.app / "pending").exists())

    def test_busy_worker_aborts_before_mutation(self):
        result = self.invoke("candidate", FAIL_DRAIN="1")
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse((self.new / "mutated").exists())
        self.assertNotIn(" up ", self.log.read_text())

    def test_success_is_not_committed_until_external_checks_finalize(self):
        result = self.invoke("candidate")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assert_status("awaiting_external_checks")
        self.assertEqual((self.app / "current").resolve(), self.old)
        result = self.invoke("finalize")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual((self.app / "current").resolve(), self.new)
        self.assertEqual((self.app / "previous").resolve(), self.old)
        self.assertFalse((self.app / "pending").exists())
        self.assert_status("deployed")

    def test_finalized_attempt_is_not_eligible_for_candidate_rollback(self):
        self.assertEqual(self.invoke("candidate").returncode, 0)
        self.assertEqual(self.invoke("finalize").returncode, 0)
        self.assertNotEqual(self.invoke("check-candidate-rollback").returncode, 0)
        self.assert_status("deployed")

    def test_failed_up_rolls_back_compatible_release(self):
        result = self.invoke("candidate", FAIL_UP="1")
        self.assertNotEqual(result.returncode, 0)
        self.assert_status("rolled_back")
        self.assertEqual((self.app / "current").resolve(), self.old)
        self.assertFalse((self.app / "pending").exists())

    def test_failed_health_rolls_back(self):
        result = self.invoke("candidate", FAIL_HEALTH="1")
        self.assertNotEqual(result.returncode, 0)
        self.assert_status("rolled_back")

    def test_schema_change_does_not_trigger_unsafe_rollback(self):
        old = json.loads((self.old / "release.json").read_text())
        old["schemas"]["ai"]["other"] = "different"
        write(self.old / "release.json", old)
        result = self.invoke("candidate", FAIL_UP="1")
        self.assertNotEqual(result.returncode, 0)
        self.assert_status("recovery_required")
        self.assertTrue((self.app / "pending").exists())

    def test_failed_rollback_keeps_pending_for_recovery(self):
        result = self.invoke("candidate", FAIL_UP="1", FAIL_ROLLBACK="1")
        self.assertNotEqual(result.returncode, 0)
        self.assert_status("recovery_required")
        self.assertTrue((self.app / "pending").exists())

    def test_modified_old_runtime_configuration_blocks_rollback(self):
        with (self.old / "runtime.env").open("a") as output:
            output.write('SMTP_HOST="modified-after-release"\n')
        result = self.invoke("candidate", FAIL_UP="1")
        self.assertNotEqual(result.returncode, 0)
        self.assert_status("recovery_required")

    def test_changed_storage_credentials_block_rollback(self):
        payload = json.loads((self.new / "settings.json").read_text())
        payload["S3_SECRET_KEY"] = "rotated-test-secret"
        write(self.new / "settings.json", payload)
        # settings.json is private, supplied after the sealed public release bundle.
        with patch("sys.argv", ["release.py", "seal", str(self.new)]):
            release_main()
        result = self.invoke("candidate", FAIL_UP="1")
        self.assertNotEqual(result.returncode, 0)
        self.assert_status("recovery_required")

    def test_external_failure_can_rollback_candidate_idempotently(self):
        self.assertEqual(self.invoke("candidate").returncode, 0)
        result = self.invoke("rollback")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assert_status("rolled_back")
        self.assertEqual(self.invoke("rollback").returncode, 0)

    def test_existing_attempt_cannot_be_overwritten(self):
        self.assertEqual(self.invoke("prepare").returncode, 0)
        before = (self.new / "runtime.env").read_bytes()
        self.assertNotEqual(self.invoke("candidate").returncode, 0)
        self.assertEqual(before, (self.new / "runtime.env").read_bytes())

    def test_vm_lock_blocks_a_parallel_deploy(self):
        (self.app / "deploy.lock").touch()
        import fcntl

        with (self.app / "deploy.lock").open("w") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            result = self.invoke("candidate")
            self.assertNotEqual(result.returncode, 0)
            self.assertFalse((self.new / "runtime.env").exists())


if __name__ == "__main__":
    unittest.main()
