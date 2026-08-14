import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src" / "MigradorSeguro.cs").read_text(encoding="utf-8")
BUILD = (ROOT / "build-native.ps1").read_text(encoding="utf-8")


class ThemeCatalogTests(unittest.TestCase):
    def test_external_themes_are_listed(self):
        for theme in ("Crux", "Newaita", "Papirus", "WhiteSur"):
            self.assertIn(f'Name="{theme}"', SOURCE)
            self.assertIn(f"Tema-Iconos-{theme}.zip", SOURCE)

    def test_theme_packages_are_not_embedded(self):
        self.assertNotIn("theme-packs", BUILD.lower())
        self.assertNotIn("janus-icons", BUILD.lower())

    def test_each_theme_has_its_own_storage(self):
        self.assertIn('"IconPacks",SelectedTheme.Id', SOURCE)
        self.assertIn('"IconThemes",SelectedTheme.Id', SOURCE)


if __name__ == "__main__":
    unittest.main()
