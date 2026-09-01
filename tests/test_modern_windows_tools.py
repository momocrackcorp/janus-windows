import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src" / "MigradorSeguro.cs").read_text(encoding="utf-8")
BUILD = (ROOT / "build-native.ps1").read_text(encoding="utf-8")


class ModernWindowsToolsTests(unittest.TestCase):
    def test_embeds_drive_desktop_component(self):
        self.assertIn("UnidadesEnEscritorio-v0.4.1-prueba.exe", BUILD)
        self.assertIn("MigradorSeguro.UnidadesEnEscritorio.exe", BUILD)
        self.assertIn("Programs\\MomoCrackCorp\\UnidadesEnEscritorio", SOURCE)
        self.assertIn("Environment.SpecialFolder.Startup", SOURCE)

    def test_disable_only_removes_managed_desktop_shortcuts(self):
        self.assertIn("accesos-administrados.txt", SOURCE)
        self.assertIn("full.StartsWith(desktop,StringComparison.OrdinalIgnoreCase)", SOURCE)
        self.assertIn('String.Equals(Path.GetExtension(full),".lnk"', SOURCE)

    def test_windows_11_actions_are_guarded(self):
        self.assertIn("CreateModernWindowsGroup(true)", SOURCE)
        self.assertIn("ms-settings:personalization-start-places", SOURCE)
        self.assertIn("ms-settings:developers", SOURCE)
        self.assertIn("IsWindows11Build(26100)", SOURCE)
        self.assertNotIn("sudo config --enable", SOURCE.lower())

    def test_tools_page_scrolls(self):
        self.assertIn("AutoScroll=true", SOURCE)
        self.assertIn("Height=900", SOURCE)


if __name__ == "__main__":
    unittest.main()
