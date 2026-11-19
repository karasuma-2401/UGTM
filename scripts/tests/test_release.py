import copy
from datetime import datetime, timedelta, timezone
from contextlib import redirect_stdout
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from release_fixtures import manifest, settings
from release import validate, dotenv, fingerprint, main as release_main
from production_metadata import compatible, initialize
from production_backup import pg_settings, env_file
from production_pipeline import validate_run


class ReleaseTests(unittest.TestCase):
    def test_source_fingerprint_cannot_confuse_content_with_file_boundaries(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "A").write_bytes(b"X")
            (root / "B").write_bytes(b"Y")
            with patch("release.ROOT", root), patch(
                "release.subprocess.check_output", return_value=b"A\0B\0"
            ):
                before = fingerprint(".")
            (root / "A").write_bytes(b"XB\0Y")
            with patch("release.ROOT", root), patch(
                "release.subprocess.check_output", return_value=b"A\0"
            ):
                self.assertNotEqual(before, fingerprint("."))

    def plan(self, previous, *arguments):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "previous.json"
            path.write_text(json.dumps(previous))
            output = io.StringIO()
            with patch.dict(
                os.environ, {"GITHUB_REF_NAME": "production", "GITHUB_SHA": "c" * 40}
            ), patch(
                "release.fingerprint",
                side_effect=lambda context: ("a" if context == "backend" else "b") * 64,
            ), patch(
                "sys.argv", ["release.py", "plan", "--previous", str(path), *arguments]
            ), redirect_stdout(
                output
            ):
                release_main()
            return json.loads(output.getvalue())["include"]

    def test_reuse_expiry_is_based_on_original_build_not_new_manifest_time(self):
        previous = manifest()
        previous["images"]["api"]["built_at"] = (
            datetime.now(timezone.utc) - timedelta(days=8)
        ).isoformat()
        jobs = self.plan(previous)
        self.assertFalse(
            next(job for job in jobs if job["component"] == "api")["reuse"]
        )
        self.assertTrue(next(job for job in jobs if job["component"] == "ai")["reuse"])

    def test_force_build_and_cross_branch_never_reuse(self):
        self.assertFalse(any(job["reuse"] for job in self.plan(manifest(), "--force")))
        previous = manifest()
        previous["branch"] = "dev"
        self.assertFalse(any(job["reuse"] for job in self.plan(previous)))

    def test_adding_a_platform_forces_native_builds_for_both(self):
        jobs = self.plan(manifest(), "--amd64")
        self.assertEqual(len(jobs), 6)
        self.assertFalse(any(job["reuse"] for job in jobs))
        self.assertEqual(
            {job["runner"] for job in jobs}, {"ubuntu-24.04", "ubuntu-24.04-arm"}
        )

    def test_valid_release_and_exact_repository(self):
        validate(manifest(), "example/ugtm", "a" * 40)
        for repository, sha in [
            ("attacker/ugtm", "a" * 40),
            ("example/ugtm", "b" * 40),
        ]:
            with self.assertRaises(ValueError):
                validate(manifest(), repository, sha)

    def test_legacy_manifest_can_plan_an_upgrade_without_reusing_an_unscanned_gateway(
        self,
    ):
        legacy = manifest()
        legacy["version"] = 1
        del legacy["images"]["gateway"]
        validate(legacy)
        jobs = self.plan(legacy)
        self.assertTrue(next(job for job in jobs if job["component"] == "api")["reuse"])
        self.assertTrue(next(job for job in jobs if job["component"] == "ai")["reuse"])
        self.assertFalse(
            next(job for job in jobs if job["component"] == "gateway")["reuse"]
        )
        self.assertFalse(compatible(legacy, manifest()))

    def test_new_manifest_requires_a_scanned_gateway_and_sealed_image_setting(self):
        missing = manifest()
        del missing["images"]["gateway"]
        with self.assertRaises(ValueError):
            validate(missing)
        unscanned = manifest()
        unscanned["images"]["gateway"]["platforms"] = {"amd64": "sha256:" + "c" * 64}
        with self.assertRaises(ValueError):
            validate(unscanned)

    def test_rejects_floating_foreign_and_missing_platform_images(self):
        for change in ("tag", "registry", "arm"):
            data = manifest()
            if change == "tag":
                data["images"]["api"]["reference"] = "ghcr.io/example/ugtm-api:latest"
            if change == "registry":
                data["images"]["api"]["reference"] = (
                    "ghcr.io/attacker/ugtm-api@sha256:" + "a" * 64
                )
            if change == "arm":
                data["images"]["api"]["platforms"] = {}
            with self.assertRaises(ValueError):
                validate(data)

    def test_deploy_requires_production_manifest(self):
        data = manifest()
        data["branch"] = "dev"
        with self.assertRaises(ValueError):
            validate(data, "example/ugtm")

    def test_schema_and_data_contract_block_automatic_rollback(self):
        previous = manifest()
        self.assertTrue(compatible(previous, manifest()))
        for field in ("schemas", "data_contract", "auto_rollback"):
            new = manifest()
            if field == "schemas":
                new["schemas"]["ai"]["002_next"] = "hash"
            else:
                new["rollback_policy"][field] = False if field == "auto_rollback" else 2
            self.assertFalse(compatible(previous, new))

    def test_dotenv_escapes_literals_and_rejects_newlines(self):
        value = 'quote" slash\\ dollar$ tab\t'
        self.assertEqual(
            dotenv({"SETTING": value}), 'SETTING="quote\\" slash\\\\ dollar$$ tab\\t"\n'
        )
        for value in ("one\ntwo", "one\rtwo", "null\0byte"):
            with self.assertRaises(ValueError):
                dotenv({"SETTING": value})

    def test_settings_are_private_single_use_and_image_references_are_not_overridable(
        self,
    ):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary) / "releases" / "attempt"
            directory.mkdir(parents=True)
            (directory / "release.json").write_text(json.dumps(manifest()))
            values = settings()
            values["API_IMAGE"] = "malicious:tag"
            values["GATEWAY_IMAGE"] = "malicious:tag"
            (directory / "settings.json").write_text(json.dumps(values))
            initialize(directory)
            self.assertFalse((directory / "settings.json").exists())
            self.assertIn(
                "ghcr.io/example/ugtm-api@sha256:",
                (directory / "runtime.env").read_text(),
            )
            self.assertNotIn("malicious:tag", (directory / "runtime.env").read_text())
            self.assertIn(
                "ghcr.io/example/ugtm-gateway@sha256:",
                (directory / "runtime.env").read_text(),
            )
            self.assertNotIn("SMTP_PASSWORD", (directory / "public.json").read_text())
            with self.assertRaises(ValueError):
                initialize(directory)

    def test_bundle_seal_detects_file_tampering_and_traversal(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "release.json").write_text("{}")
            with patch("sys.argv", ["release.py", "seal", temporary]):
                release_main()
            with patch("sys.argv", ["release.py", "verify", temporary]):
                release_main()
            (root / "release.json").write_text('{"tampered":true}')
            with self.assertRaises(ValueError), patch(
                "sys.argv", ["release.py", "verify", temporary]
            ):
                release_main()
            (root / "checksums.json").write_text(json.dumps({"../outside": "a" * 64}))
            with self.assertRaises(ValueError), patch(
                "sys.argv", ["release.py", "verify", temporary]
            ):
                release_main()

    def test_publish_evidence_rejects_failed_fork_wrong_workflow_and_nonancestor(self):
        valid = {
            "conclusion": "success",
            "head_branch": "production",
            "path": ".github/workflows/publish.yml",
            "repository": {"full_name": "example/ugtm"},
        }
        validate_run(valid, "example/ugtm", {"status": "ahead"})
        for field, value in [
            ("conclusion", "failure"),
            ("head_branch", "dev"),
            ("path", ".github/workflows/other.yml"),
            ("repository", {"full_name": "attacker/ugtm"}),
        ]:
            modified = {**valid, field: value}
            with self.assertRaises(ValueError):
                validate_run(modified, "example/ugtm", {"status": "identical"})
        with self.assertRaises(ValueError):
            validate_run(valid, "example/ugtm", {"status": "diverged"})

    def test_backup_connections_are_direct_and_preserve_encoded_passwords(self):
        result = pg_settings(
            "postgresql://user:p%40ss%3Aword@direct.example.test/neondb?sslmode=verify-full"
        )
        self.assertEqual(result["PGPASSWORD"], "p@ss:word")
        self.assertEqual(result["PGSSLMODE"], "verify-full")
        with self.assertRaises(ValueError):
            pg_settings("postgresql://u:p@ep-test-pooler.example.test/db")
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaises(ValueError):
                env_file(Path(temporary) / "settings.env", {"PASSWORD": "bad\nvalue"})


if __name__ == "__main__":
    unittest.main()
