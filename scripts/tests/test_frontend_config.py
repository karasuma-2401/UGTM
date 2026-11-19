"""Evaluate the deployment config in a fresh directory without node_modules."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

from release_fixtures import ROOT


class FrontendConfigTests(unittest.TestCase):
    def evaluate(self, origin):
        with tempfile.TemporaryDirectory(prefix="ugtm-vercel-config-") as temporary:
            directory = Path(temporary)
            config = directory / "vercel.ts"
            shutil.copyfile(ROOT / "frontend/vercel.ts", config)
            (directory / "package.json").write_text('{"type":"module"}')
            environment = dict(os.environ)
            environment.pop("API_ORIGIN", None)
            if origin is not None:
                environment["API_ORIGIN"] = origin
            return subprocess.run(
                [
                    "node",
                    "--input-type=module",
                    "--eval",
                    f"const module = await import({json.dumps(config.as_uri())}); "
                    "console.log(JSON.stringify(module.config));",
                ],
                cwd=directory,
                env=environment,
                capture_output=True,
                text=True,
                timeout=30,
            )

    def test_cold_source_loads_without_npm_dependencies_and_preserves_routes(self):
        result = self.evaluate("https://api.example.test")
        self.assertEqual(result.returncode, 0, result.stderr)
        config = json.loads(result.stdout)
        self.assertEqual(
            config["rewrites"],
            [
                {
                    "source": "/api/(.*)",
                    "destination": "$API_ORIGIN/api/$1",
                    "env": ["API_ORIGIN"],
                },
                {"source": "/(.*)", "destination": "/index.html"},
            ],
        )
        self.assertIs(config["git"]["deploymentEnabled"], False)

    def test_missing_api_origin_is_rejected_before_upload(self):
        result = self.evaluate(None)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("API_ORIGIN must be configured", result.stderr)

    def test_credentials_paths_and_non_https_origins_are_rejected(self):
        for origin in (
            "http://api.example.test",
            "https://user:password@api.example.test",
            "https://api.example.test/path",
            "https://api.example.test/?query=1",
            "https://api.example.test/#fragment",
        ):
            with self.subTest(origin=origin):
                result = self.evaluate(origin)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("API_ORIGIN must be an HTTPS origin", result.stderr)
