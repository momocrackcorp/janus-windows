import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = (ROOT / "tools" / "package-janus-plus-beta.ps1").read_text(encoding="utf-8")
NOTES = (ROOT / "RELEASE-NOTES-4.0-BETA.md").read_text(encoding="utf-8")


class JanusPlusPackagingTests(unittest.TestCase):
    def test_beta_package_has_stable_release_identity(self):
        self.assertIn('Version = "4.0.0-beta"', SCRIPT)
        self.assertIn('"JANUS-Plus-$Version"', SCRIPT)
        self.assertIn("JANUS+ 4.0 Beta", NOTES)

    def test_package_contains_binary_docs_notes_and_hash(self):
        self.assertIn("build-native.ps1", SCRIPT)
        self.assertIn('"README.md"', SCRIPT)
        self.assertIn('"RELEASE-NOTES-4.0-BETA.md"', SCRIPT)
        self.assertIn('"SHA256SUMS.txt"', SCRIPT)
        self.assertIn('"$baseName-SHA256.txt"', SCRIPT)
        self.assertIn("Get-FileHash", SCRIPT)
        self.assertIn("Compress-Archive", SCRIPT)

    def test_external_themes_are_not_bundled(self):
        self.assertNotIn("assets\\windows-themes", SCRIPT)
        self.assertNotIn("assets\\theme-packs", SCRIPT)
        self.assertIn("no vienen incorporados", NOTES)


if __name__ == "__main__":
    unittest.main()
