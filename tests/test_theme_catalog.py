import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src" / "MigradorSeguro.cs").read_text(encoding="utf-8")
BUILD = (ROOT / "build-native.ps1").read_text(encoding="utf-8")


class ThemeCatalogTests(unittest.TestCase):
    def test_external_themes_are_listed(self):
        for name, package in (
            ("Crux", "Crux"),
            ("Newaita", "Newaita"),
            ("Papirus", "Papirus"),
            ("WhiteSur", "WhiteSur"),
            ("La Capitaine", "La-Capitaine"),
        ):
            self.assertIn(f'Name="{name}"', SOURCE)
            self.assertIn(f"Tema-Iconos-{package}.zip", SOURCE)

    def test_theme_packages_are_not_embedded(self):
        self.assertNotIn("theme-packs", BUILD.lower())
        self.assertNotIn("janus-icons", BUILD.lower())

    def test_each_theme_has_its_own_storage(self):
        self.assertIn('"IconPacks",SelectedTheme.Id', SOURCE)
        self.assertIn('"IconThemes",SelectedTheme.Id', SOURCE)

    def test_folder_icon_changes_refresh_quick_access(self):
        self.assertIn("NotifyFolderIconChanged(folder)", SOURCE)
        self.assertIn("NotifyFolderIconChanged(item.Item2)", SOURCE)
        self.assertIn('EntryPoint="SHChangeNotify"', SOURCE)

    def test_quick_access_icons_are_backed_up_and_reversible(self):
        self.assertIn("ApplyQuickAccessIcon(item.Item4,iconValue)", SOURCE)
        self.assertIn("result[LegacyKeyPath(folder.Item4)]", SOURCE)
        self.assertIn("if(!original.ContainsKey(entry.Key))", SOURCE)
        for clsid in (
            "A8CDFF1C-4878-43BE-B5FD-F8091C1C60D0",
            "374DE290-123F-4565-9164-39C4925E467B",
            "B4BFCC3A-DB2C-424C-B029-7FE99A87C641",
            "3ADD1653-EB32-4CB0-BBD7-DFA0ABB5ACCA",
            "1CF1260C-4DD0-4EBB-811F-33C572699FDE",
            "A0953C92-50DC-43BF-BE83-3742FED03C9C",
        ):
            self.assertIn(clsid, SOURCE)


if __name__ == "__main__":
    unittest.main()
