import unittest
from unittest.mock import patch

from release_fixtures import ROOT
import production_operations as operations


class OperationsTests(unittest.TestCase):
    record = {
        "vercel_url": "https://candidate.vercel.app",
        "previous_vercel_url": "https://previous.vercel.app",
    }

    def test_recovery_is_idempotent_if_frontend_was_already_restored(self):
        with patch.object(
            operations,
            "previous_frontend",
            return_value=self.record["previous_vercel_url"],
        ), patch.object(operations, "vercel") as command:
            operations.rollback_frontend(self.record)
            command.assert_not_called()

    def test_recovery_only_reverts_the_deployment_owned_by_this_attempt(self):
        with patch.object(
            operations, "previous_frontend", return_value=self.record["vercel_url"]
        ), patch.object(operations, "vercel") as command:
            operations.rollback_frontend(self.record)
            command.assert_called_once_with(
                "rollback", self.record["previous_vercel_url"], "--yes"
            )

    def test_a_newer_external_deployment_is_not_overwritten(self):
        with patch.object(
            operations, "previous_frontend", return_value="https://different.vercel.app"
        ), patch.object(operations, "vercel") as command:
            with self.assertRaises(ValueError):
                operations.rollback_frontend(self.record)
            command.assert_not_called()
