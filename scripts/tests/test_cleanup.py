from datetime import datetime, timezone
import json
import os
from pathlib import Path
import tempfile
import unittest

from release_fixtures import ROOT
from production_cleanup import candidates


@unittest.skipIf(
    os.name == "nt",
    "Linux symlink retention contracts run in the temporary Linux test container",
)
class CleanupTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.releases = self.root / "releases"
        self.releases.mkdir()
        self.directories = []
        for index in range(10):
            path = self.releases / ("a" * 40 + f"-{index}-1")
            path.mkdir()
            (path / "status.json").write_text(json.dumps({"status": "deployed"}))
            timestamp = datetime.now(timezone.utc).timestamp() - (
                60 * 86400 + index * 60
            )
            os.utime(path, (timestamp, timestamp))
            self.directories.append(path)
        (self.root / "current").symlink_to(self.directories[-1])
        (self.root / "previous").symlink_to(self.directories[-2])

    def test_retains_current_previous_and_five_latest_successes(self):
        self.assertEqual(set(candidates(self.root)), set(self.directories[5:8]))

    def test_pending_or_failed_releases_are_not_removed(self):
        (self.directories[5] / "status.json").write_text(
            '{"status":"recovery_required"}'
        )
        self.assertNotIn(self.directories[5], candidates(self.root))
        (self.root / "pending").write_text(str(self.directories[6]))
        with self.assertRaises(ValueError):
            candidates(self.root)

    def test_symlinks_to_other_directories_are_never_followed(self):
        outside = self.root / "outside"
        outside.mkdir()
        (outside / "status.json").write_text('{"status":"deployed"}')
        (self.releases / ("b" * 40 + "-100-1")).symlink_to(outside)
        self.assertNotIn(outside, candidates(self.root))

    def test_recent_releases_are_retained(self):
        self.assertEqual(candidates(self.root, days=100), [])
