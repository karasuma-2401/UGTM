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
            with patch("sys.argv", ["release.py", "seal", str(directory)]):
                release_main()
            write(directory / "settings.json", settings())
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
if args[0]=='inspect' and 'com.docker.compose.project.config_files' in ' '.join(args): print(os.environ.get('FAKE_RECOVERY_CONFIG', 'unexpected'))
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

    def recovery(self):
        legacy = self.app / "releases" / "legacy"
        legacy.mkdir()
        shutil.copy(self.old / "runtime.env", legacy / "runtime.env")
        (self.app / "current").unlink()
        (self.app / "current").symlink_to(legacy)
        write(self.old / "status.json", {"status": "recovery_required", "events": []})
        (self.old / "mutated").touch()
        (self.app / "pending").write_text(str(self.old) + "\n")
        (self.new / "recovery-from").write_text(self.old.name + "\n")
        self.env["FAKE_RECOVERY_CONFIG"] = str(
            self.old / "docker/docker-compose.prod.yml"
        )
        # The DB has an additional migration; only the replacement image owns it.
        replacement = json.loads((self.new / "release.json").read_text())
        replacement["schemas"]["ugtm"]["AddRoleIsHidden"] = "new-schema"
        write(self.new / "release.json", replacement)
        with patch("sys.argv", ["release.py", "seal", str(self.new)]):
            release_main()
        return legacy

    def test_forward_recovery_requires_explicit_matching_attempt(self):
        self.recovery()
        (self.new / "recovery-from").unlink()
        self.assertNotEqual(self.invoke("candidate").returncode, 0)
        self.assertEqual((self.app / "pending").read_text().strip(), str(self.old))
        self.assertFalse((self.new / "runtime.env").exists())

    def test_forward_recovery_rejects_a_different_running_stack(self):
        self.recovery()
        result = self.invoke("candidate", FAKE_RECOVERY_CONFIG="/different/compose.yml")
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual((self.app / "pending").read_text().strip(), str(self.old))
        self.assertNotIn(" up ", self.log.read_text())

    def test_forward_recovery_schema_failure_preserves_old_pending(self):
        legacy = self.recovery()
        result = self.invoke("candidate", FAIL_SCHEMA="1")
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual((self.app / "pending").read_text().strip(), str(self.old))
        self.assertEqual((self.app / "current").resolve(), legacy)
        self.assertEqual(
            json.loads((self.old / "status.json").read_text())["status"],
            "recovery_required",
        )
        self.assertTrue((self.old / "mutated").exists())
        self.assertNotIn(" up ", self.log.read_text())

    def test_forward_recovery_does_not_change_credentials_or_storage(self):
        self.recovery()
        payload = json.loads((self.new / "settings.json").read_text())
        payload["S3_SECRET_KEY"] = "rotated-test-secret"
        write(self.new / "settings.json", payload)
        with patch("sys.argv", ["release.py", "seal", str(self.new)]):
            release_main()
        self.assertNotEqual(self.invoke("candidate").returncode, 0)
        self.assertEqual((self.app / "pending").read_text().strip(), str(self.old))
        self.assertNotIn(" up ", self.log.read_text())

    def test_forward_recovery_rejects_modified_private_configuration(self):
        self.recovery()
        with (self.old / "runtime.env").open("a") as output:
            output.write('SMTP_HOST="modified-after-attempt"\n')
        self.assertNotEqual(self.invoke("candidate").returncode, 0)
        self.assertEqual((self.app / "pending").read_text().strip(), str(self.old))
        self.assertNotIn(" up ", self.log.read_text())

    def test_forward_recovery_rejects_changed_data_contract(self):
        self.recovery()
        replacement = json.loads((self.new / "release.json").read_text())
        replacement["rollback_policy"]["data_contract"] += 1
        write(self.new / "release.json", replacement)
        with patch("sys.argv", ["release.py", "seal", str(self.new)]):
            release_main()
        self.assertNotEqual(self.invoke("candidate").returncode, 0)
        self.assertEqual((self.app / "pending").read_text().strip(), str(self.old))
        self.assertNotIn(" up ", self.log.read_text())

    def test_forward_recovery_drains_the_running_worker_before_handover(self):
        self.recovery()
        self.assertNotEqual(self.invoke("candidate", FAIL_DRAIN="1").returncode, 0)
        self.assertEqual((self.app / "pending").read_text().strip(), str(self.old))
        self.assertFalse((self.old / "superseded-by").exists())
        self.assertNotIn(" up ", self.log.read_text())

    def test_forward_recovery_keeps_audit_and_waits_for_finalize(self):
        legacy = self.recovery()
        result = self.invoke("candidate")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual((self.app / "current").resolve(), legacy)
        self.assertEqual(
            (self.new / "previous-path").read_text().strip(), str(self.old)
        )
        self.assertEqual((self.app / "pending").read_text().strip(), str(self.new))
        self.assertEqual(
            (self.old / "superseded-by").read_text().strip(), str(self.new)
        )
        self.assertEqual(
            json.loads((self.old / "status.json").read_text())["status"], "superseded"
        )
        self.assertFalse((self.old / "mutated").exists())
        self.assert_status("awaiting_external_checks")
        self.assertEqual(self.invoke("finalize").returncode, 0)
        self.assertEqual((self.app / "current").resolve(), self.new)

    def test_forward_recovery_failure_holds_new_pending_without_schema_rollback(self):
        self.recovery()
        self.assertNotEqual(self.invoke("candidate", FAIL_UP="1").returncode, 0)
        self.assertEqual((self.app / "pending").read_text().strip(), str(self.new))
        self.assert_status("recovery_required")
        calls = self.log.read_text()
        self.assertNotIn(
            f"--env-file {self.old}/runtime.env -f {self.old}/docker/docker-compose.prod.yml up",
            calls,
        )

    def test_forward_recovery_never_rolls_back_to_an_unfinished_pair_even_if_schemas_match(
        self,
    ):
        self.recovery()
        replacement = json.loads((self.new / "release.json").read_text())
        replacement["schemas"] = json.loads((self.old / "release.json").read_text())[
            "schemas"
        ]
        write(self.new / "release.json", replacement)
        with patch("sys.argv", ["release.py", "seal", str(self.new)]):
            release_main()
        self.assertNotEqual(self.invoke("candidate", FAIL_UP="1").returncode, 0)
        self.assertEqual((self.app / "pending").read_text().strip(), str(self.new))
        self.assert_status("recovery_required")
        self.assertNotEqual(self.invoke("check-candidate-rollback").returncode, 0)

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
