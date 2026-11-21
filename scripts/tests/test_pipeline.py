import io
import json
import os
import subprocess
import urllib.parse
from pathlib import Path
import shutil
import tarfile
import tempfile
import unittest
from unittest.mock import patch

from release_fixtures import ROOT, manifest, settings
import production_pipeline as pipeline


class PipelineTests(unittest.TestCase):
    def test_forward_recovery_requires_manual_deploy_and_valid_attempt_id(self):
        for mode, automatic, attempt in (
            ("prepare", "false", "a" * 40 + "-1-1"),
            ("deploy", "true", "a" * 40 + "-1-1"),
            ("deploy", "false", "../../other"),
        ):
            with self.subTest(
                mode=mode, automatic=automatic, attempt=attempt
            ), patch.dict(
                os.environ,
                {
                    "GITHUB_REPOSITORY": "example/ugtm",
                    "DEPLOY_MODE": mode,
                    "AUTO_DEPLOY": automatic,
                    "RECOVERY_ATTEMPT": attempt,
                },
                clear=True,
            ), patch.object(
                pipeline, "github"
            ) as github:
                with self.assertRaisesRegex(ValueError, "RECOVERY_ATTEMPT"):
                    pipeline.deploy(Path("unused"))
                github.assert_not_called()

    def test_deploy_provides_the_same_api_origin_to_config_build_and_routing(self):
        with patch.dict(
            os.environ,
            {
                "API_HOST": "api.example.test",
                "API_ORIGIN": "https://stale.example.test",
                "VERCEL_TOKEN": "test-only",
                "VERCEL_ORG_ID": "team_test",
            },
        ), patch.object(
            pipeline, "run", return_value="https://candidate.vercel.app"
        ) as run:
            pipeline.vercel("deploy", "--prod", "--skip-domain", cwd=Path("source"))
        args = run.call_args.args[0]
        self.assertIn("--skip-domain", args)
        self.assertEqual(
            args[args.index("--build-env") + 1], "API_ORIGIN=https://api.example.test"
        )
        self.assertEqual(
            args[args.index("--env") + 1], "API_ORIGIN=https://api.example.test"
        )
        self.assertEqual(
            run.call_args.kwargs["env"]["API_ORIGIN"], "https://api.example.test"
        )

    def test_rollback_does_not_require_backend_environment_settings(self):
        with patch.dict(
            os.environ,
            {"VERCEL_TOKEN": "test-only", "VERCEL_ORG_ID": "team_test"},
            clear=True,
        ), patch.object(pipeline, "run", return_value="") as run:
            pipeline.vercel("rollback", "https://previous.vercel.app", "--yes")
        self.assertNotIn("--build-env", run.call_args.args[0])

    def test_all_vercel_commands_pin_scope_without_a_linked_directory(self):
        for command in ("deploy", "inspect", "promote", "rollback"):
            with self.subTest(command=command), patch.dict(
                os.environ,
                {
                    "API_HOST": "api.example.test",
                    "VERCEL_TOKEN": "test-only",
                    "VERCEL_ORG_ID": "team_intended",
                },
                clear=True,
            ), patch.object(pipeline, "run", return_value="") as run:
                pipeline.vercel(command, "https://candidate.vercel.app")
                args = run.call_args.args[0]
                self.assertEqual(args[args.index("--scope") + 1], "team_intended")
                self.assertIsNone(run.call_args.kwargs["cwd"])

    def test_candidate_scope_check_rejects_wrong_url_not_ready_and_preview(self):
        valid = {
            "url": "candidate.vercel.app",
            "readyState": "READY",
            "target": "production",
        }
        for changes in (
            {"url": "other.vercel.app"},
            {"readyState": "ERROR"},
            {"target": "preview"},
        ):
            with self.subTest(changes=changes), patch.object(
                pipeline, "vercel", return_value=json.dumps({**valid, **changes})
            ):
                with self.assertRaises(ValueError):
                    pipeline.verify_frontend_scope("https://candidate.vercel.app")

    def test_invalid_scope_is_rejected_without_running_the_cli(self):
        with patch.dict(
            os.environ,
            {"VERCEL_ORG_ID": "team_invalid scope", "VERCEL_TOKEN": "test-only"},
            clear=True,
        ), patch.object(pipeline, "run") as run:
            with self.assertRaisesRegex(ValueError, "VERCEL_ORG_ID"):
                pipeline.vercel("promote", "https://candidate.vercel.app", "--yes")
            run.assert_not_called()

    def test_vercel_error_keeps_the_cause_without_leaking_secrets_or_build_output(self):
        token = "synthetic/token+with special=characters"
        stderr = (
            "build output that must not be logged\n"
            f"\x1b[31mError: API_ORIGIN missing; token={token}; encoded={urllib.parse.quote(token, safe='')}; "
            "postgresql://user:password@db.example.test/db; ::warning::payload\x1b[0m\n"
        )
        with patch.dict(os.environ, {"VERCEL_TOKEN": token}), patch.object(
            pipeline.subprocess,
            "run",
            return_value=subprocess.CompletedProcess(
                [], 1, stdout="private stdout", stderr=stderr
            ),
        ):
            with self.assertRaises(RuntimeError) as raised:
                pipeline.run(["vercel", "deploy", "--token", token])
        message = str(raised.exception)
        self.assertIn("API_ORIGIN missing", message)
        for private in (
            token,
            urllib.parse.quote(token, safe=""),
            "user:password",
            "private stdout",
            "build output",
            "::warning::",
            "\x1b",
        ):
            self.assertNotIn(private, message)

    def execute(self, directory, fail=None, recovery=False):
        source = directory / "artifact"
        source.mkdir()
        (source / "scripts").mkdir()
        (source / "release.json").write_text(json.dumps(manifest()))
        (source / "scripts/release.py").write_text(
            "# fixture verified by separate bundle tests"
        )
        archive = io.BytesIO()
        with tarfile.open(fileobj=archive, mode="w"):
            pass
        environment = {
            **settings(),
            "GITHUB_REPOSITORY": "example/ugtm",
            "PUBLISH_RUN_ID": "100",
            "GITHUB_RUN_ID": "200",
            "GITHUB_RUN_ATTEMPT": "1",
            "DEPLOYMENT_SCOPE": "production",
            "GH_TOKEN": "test-only",
            "VPS_HOST": "host.example.test",
            "VPS_USERNAME": "test",
            "VPS_SSH_KEY": "test-only",
            "VPS_KNOWN_HOSTS": "test-only",
            "VERCEL_TOKEN": "test-only",
            "VERCEL_ORG_ID": "team_test",
            "VERCEL_PROJECT_ID": "project_test",
            "SMOKE_AUTH_ENABLED": "false",
            "RECOVERY_ATTEMPT": "b" * 40 + "-1-1" if recovery else "",
        }
        info = {
            "head_sha": "a" * 40,
            "run_attempt": 1,
            "conclusion": "success",
            "head_branch": "production",
            "path": ".github/workflows/publish.yml",
            "repository": {"full_name": "example/ugtm"},
        }
        phases = []
        commands = []
        status = {"status": "awaiting_external_checks"}

        class Remote:
            def __init__(self, workspace):
                pass

            def execute(self, command, **kwargs):
                if "production/pending" in command:
                    return (
                        "/home/test/ugtm/production/releases/" + "b" * 40 + "-1-1"
                        if recovery or fail == "pending"
                        else ""
                    )
                if "deployment.json" in command:
                    return json.dumps(
                        {
                            "attempt": "b" * 40 + "-1-1",
                            "source_sha": "b" * 40,
                            "vercel_url": "https://candidate.vercel.app",
                            "previous_vercel_url": (
                                "https://other.vercel.app"
                                if fail == "foreign_frontend"
                                else "https://previous.vercel.app"
                            ),
                        }
                    )
                if "status.json" in command:
                    return json.dumps(status)
                return ""

            def upload(self, *args):
                pass

            def phase(self, path, phase):
                phases.append(phase)
                if (
                    phase in ("check-candidate-rollback", "rollback")
                    and fail == "unsafe_recovery"
                ):
                    raise RuntimeError("rollback compatibility not confirmed")
                if phase == "finalize":
                    status["status"] = "deployed"
                    if fail == "finalize_ack":
                        raise RuntimeError("lost response")
                if phase == "candidate" and fail == "candidate":
                    raise RuntimeError("candidate failed")

        def run(args, **kwargs):
            if args[:3] == ["gh", "run", "download"]:
                shutil.copytree(source, Path(args[-1]))
            return ""

        def vercel(*args, **kwargs):
            commands.append(args[0])
            if args[0] == "deploy":
                return "https://candidate.vercel.app"
            if args[0] == "inspect":
                if fail == "scope":
                    raise RuntimeError("User not found. (404)")
                return json.dumps(
                    {
                        "url": "candidate.vercel.app",
                        "readyState": "READY",
                        "target": "production",
                    }
                )
            if args[0] == "promote" and fail in ("promote", "unsafe_recovery"):
                raise RuntimeError("promotion response lost")
            return ""

        workspace = directory / "workspace"
        workspace.mkdir()
        with patch.dict(os.environ, environment), patch.object(
            pipeline, "github", side_effect=[info, {"status": "identical"}]
        ), patch.object(pipeline, "run", side_effect=run), patch.object(
            pipeline, "Remote", Remote
        ), patch.object(
            pipeline, "vercel", side_effect=vercel
        ), patch.object(
            pipeline, "previous_frontend", return_value="https://previous.vercel.app"
        ), patch.object(
            pipeline, "health"
        ), patch.object(
            pipeline.subprocess, "check_output", return_value=archive.getvalue()
        ):
            try:
                pipeline.deploy(workspace)
                raised = False
            except RuntimeError:
                raised = True
        return (
            phases,
            commands,
            raised,
            json.loads((workspace / "summary.json").read_text()),
        )

    def test_frontend_is_promoted_only_after_candidate_and_checks(self):
        with tempfile.TemporaryDirectory() as directory:
            phases, commands, raised, summary = self.execute(Path(directory))
            self.assertFalse(raised)
            self.assertEqual(phases, ["candidate", "finalize"])
            self.assertEqual(commands, ["deploy", "inspect", "promote"])
            self.assertEqual(summary["result"], "deployed")

    def test_manual_forward_recovery_records_the_attempt_and_keeps_checks(self):
        with tempfile.TemporaryDirectory() as directory:
            phases, commands, raised, summary = self.execute(
                Path(directory), recovery=True
            )
            self.assertFalse(raised)
            self.assertEqual(phases, ["candidate", "finalize"])
            self.assertEqual(commands, ["deploy", "inspect", "promote"])
            self.assertEqual(summary["recovery_from"], "b" * 40 + "-1-1")

    def test_forward_recovery_does_not_overwrite_a_newer_frontend(self):
        with tempfile.TemporaryDirectory() as directory:
            # Validation errors propagate before any candidate mutation.
            with self.assertRaisesRegex(ValueError, "Vercel changed"):
                self.execute(Path(directory), "foreign_frontend", recovery=True)

    def test_pending_attempt_is_not_automatically_replaced(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(ValueError, "unfinished deployment"):
                self.execute(Path(directory), "pending")

    def test_candidate_failure_never_promotes_frontend(self):
        with tempfile.TemporaryDirectory() as directory:
            phases, commands, raised, summary = self.execute(
                Path(directory), "candidate"
            )
            self.assertTrue(raised)
            self.assertEqual(phases, ["candidate", "rollback"])
            self.assertEqual(commands, ["deploy", "inspect"])

    def test_cli_scope_failure_stops_before_backend_candidate_or_promotion(self):
        with tempfile.TemporaryDirectory() as directory:
            phases, commands, raised, summary = self.execute(Path(directory), "scope")
            self.assertTrue(raised)
            self.assertEqual(phases, [])
            self.assertEqual(commands, ["deploy", "inspect"])
            self.assertEqual(summary["result"], "failed")

    def test_uncertain_promotion_response_restores_both_sides(self):
        with tempfile.TemporaryDirectory() as directory:
            phases, commands, raised, summary = self.execute(Path(directory), "promote")
            self.assertTrue(raised)
            self.assertEqual(
                phases, ["candidate", "check-candidate-rollback", "rollback"]
            )
            self.assertEqual(commands, ["deploy", "inspect", "promote", "rollback"])

    def test_uncertain_backend_state_does_not_blindly_revert_frontend(self):
        with tempfile.TemporaryDirectory() as directory:
            phases, commands, raised, summary = self.execute(
                Path(directory), "unsafe_recovery"
            )
            self.assertTrue(raised)
            self.assertEqual(commands, ["deploy", "inspect", "promote"])
            self.assertTrue(summary["recovery_errors"])

    def test_lost_finalize_response_reconciles_without_undoing_committed_release(self):
        with tempfile.TemporaryDirectory() as directory:
            phases, commands, raised, summary = self.execute(
                Path(directory), "finalize_ack"
            )
            self.assertFalse(raised)
            self.assertEqual(phases, ["candidate", "finalize"])
            self.assertNotIn("rollback", commands)
            self.assertEqual(summary["result"], "deployed")
