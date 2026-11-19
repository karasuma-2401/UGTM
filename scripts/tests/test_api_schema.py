"""Black-box tests of the published API's read-only schema command using a disposable DB."""

import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import unittest
from uuid import uuid4

if __name__ == "__main__" and len(sys.argv) == 2:
    os.environ["UGTM_SCHEMA_TEST_DLL"] = str(Path(sys.argv[1]).resolve())

ROOT = Path(__file__).resolve().parents[2]


@unittest.skipUnless(
    os.environ.get("UGTM_SCHEMA_TEST_DLL") and shutil.which("dotnet"),
    "Requires the compiled API and explicit disposable DB test invocation",
)
class ApiSchemaTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.container = "ugtm-schema-test-" + uuid4().hex[:12]
        cls.password = "synthetic-schema-test-only"
        subprocess.run(
            [
                "docker",
                "run",
                "-d",
                "--name",
                cls.container,
                "--memory",
                "384m",
                "-e",
                f"POSTGRES_PASSWORD={cls.password}",
                "-e",
                "POSTGRES_DB=schema_test",
                "-p",
                "127.0.0.1::5432",
                "postgres:18-alpine",
            ],
            check=True,
            capture_output=True,
        )
        cls.addClassCleanup(
            lambda: subprocess.run(
                ["docker", "rm", "-fv", cls.container], capture_output=True
            )
        )
        # The image starts a socket-only server during initdb. Wait for the
        # final TCP listener and verify authentication to the test database.
        deadline = time.monotonic() + 60
        while True:
            ready = cls.psql("-c", "SELECT 1")
            if ready.returncode == 0:
                break
            if time.monotonic() >= deadline:
                raise RuntimeError(
                    f"Disposable schema-test database did not become ready: {ready.stderr.strip()}"
                )
            time.sleep(1)
        port = (
            subprocess.check_output(
                ["docker", "port", cls.container, "5432"], text=True
            )
            .strip()
            .rsplit(":", 1)[1]
        )
        cls.connection = f"Host=127.0.0.1;Port={port};Database=schema_test;Username=postgres;Password={cls.password};SSL Mode=Disable;Timeout=3"
        cls.expected = []
        for file in (ROOT / "backend/Database.Migrations/Migrations").glob("*.cs"):
            cls.expected.extend(
                re.findall(r'\[Migration\("([A-Za-z0-9_]+)"\)\]', file.read_text())
            )
        assert cls.expected

    @classmethod
    def psql(cls, *args, sql=None):
        return subprocess.run(
            [
                "docker",
                "exec",
                "-i",
                "-e",
                f"PGPASSWORD={cls.password}",
                "-e",
                "PGCONNECT_TIMEOUT=3",
                cls.container,
                "psql",
                "-XAtw",
                "-h",
                "127.0.0.1",
                "-U",
                "postgres",
                "-d",
                "schema_test",
                "-v",
                "ON_ERROR_STOP=1",
                *args,
            ],
            input=sql,
            text=True,
            capture_output=True,
            timeout=15,
        )

    def history(self, migrations):
        sql = "DROP TABLE IF EXISTS public.__ef_migrations_history; CREATE TABLE public.__ef_migrations_history (migration_id varchar(150) PRIMARY KEY,product_version varchar(32) NOT NULL);"
        for migration in migrations:
            sql += f"INSERT INTO public.__ef_migrations_history VALUES ('{migration}','10.0.12');"
        result = self.psql(sql=sql)
        self.assertEqual(result.returncode, 0, result.stderr)

    def check(self):
        result = subprocess.run(
            ["dotnet", os.environ["UGTM_SCHEMA_TEST_DLL"], "--check-schema"],
            env={
                **os.environ,
                "ConnectionStrings__DefaultConnection": self.connection,
                "ASPNETCORE_ENVIRONMENT": "Production",
            },
            capture_output=True,
            text=True,
            timeout=75,
        )
        self.assertNotIn(self.password, result.stdout + result.stderr)
        return result

    def test_exact_history_passes(self):
        self.history(self.expected)
        result = self.check()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_pending_migration_is_rejected(self):
        self.history(self.expected[:-1])
        result = self.check()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn(f"Pending migrations: {self.expected[-1]}", result.stderr)

    def test_future_migration_is_rejected(self):
        self.history([*self.expected, "99999999999999_FutureMigration"])
        result = self.check()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn(
            "Migrations outside this release: 99999999999999_FutureMigration",
            result.stderr,
        )

    def test_history_is_not_created_or_modified(self):
        self.history([])
        result = self.check()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("UGTM schema does not match this release", result.stderr)
        count = self.psql("-c", "SELECT count(*) FROM public.__ef_migrations_history")
        self.assertEqual(count.returncode, 0, count.stderr)
        self.assertEqual(count.stdout.strip(), "0")


if __name__ == "__main__":
    unittest.main(argv=[sys.argv[0]])
