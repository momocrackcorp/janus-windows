import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src" / "ReinstallationCenter.cs").read_text(encoding="utf-8")
MAIN = (ROOT / "src" / "MigradorSeguro.cs").read_text(encoding="utf-8")
BUILD = (ROOT / "build-native.ps1").read_text(encoding="utf-8")


class ReinstallationCenterTests(unittest.TestCase):
    def test_center_is_built_and_reachable(self):
        self.assertIn("ReinstallationCenter.cs", BUILD)
        self.assertIn("new ReinstallationCenterForm()", MAIN)
        for tab in ("Mochila", "Puesta a punto", "Perfiles", "Respaldos e historial"):
            self.assertIn(f'new TabPage("{tab}")', SOURCE)

    def test_winget_export_import_have_preview_and_confirmation(self):
        self.assertIn('winget","export -o ', SOURCE)
        self.assertIn('"winget","import -i ', SOURCE)
        self.assertIn("PackagePreviewForm", SOURCE)
        self.assertIn("¿Continuar?", SOURCE)
        self.assertIn("--ignore-unavailable", SOURCE)

    def test_driver_backup_uses_official_pnputil_commands(self):
        self.assertIn('"/export-driver * "', SOURCE)
        self.assertIn('"/add-driver "', SOURCE)
        self.assertIn('Verb="runas"', SOURCE)
        self.assertIn("*.inf", SOURCE)
        self.assertIn("PrepareFullBag", SOURCE)
        self.assertIn("LEEME.txt", SOURCE)

    def test_operations_are_logged_and_backups_are_not_deleted(self):
        self.assertIn("historial.jsonl", SOURCE)
        self.assertIn("JanusRecovery.Record", MAIN)
        self.assertNotIn("Directory.Delete", SOURCE)
        self.assertNotIn("File.Delete(path)", SOURCE)

    def test_known_folder_restore_uses_supported_api(self):
        self.assertIn("SHSetKnownFolderPath", SOURCE)
        self.assertIn("Rutas personales que serán restauradas", SOURCE)
        self.assertIn("no moverá ni borrará archivos", SOURCE)

    def test_post_installation_check_and_official_settings(self):
        for uri in (
            "ms-settings:activation",
            "ms-settings:windowsupdate",
            "ms-settings:windowsupdate-optionalupdates",
            "ms-settings:startupapps",
            "ms-settings:defaultapps",
            "ms-settings:storagesense",
            "ms-settings:privacy",
        ):
            self.assertIn(uri, SOURCE)
        self.assertIn("CompareSelectedProfile", SOURCE)
        self.assertIn("Respaldo de controladores", SOURCE)
        self.assertIn("Microsoft Teams", SOURCE)
        self.assertIn("Microsoft OneDrive", SOURCE)

    def test_report_excludes_sensitive_material(self):
        self.assertIn("no contiene claves de producto, contraseñas", SOURCE)
        self.assertNotIn("DigitalProductId", SOURCE)
        self.assertNotIn("password", SOURCE.lower())

    def test_rc3_version_is_visible(self):
        self.assertIn('DisplayVersion="2.3.0-rc.3"', MAIN)
        self.assertIn('AssemblyInformationalVersion("2.3.0-rc.3")', MAIN)


if __name__ == "__main__":
    unittest.main()
