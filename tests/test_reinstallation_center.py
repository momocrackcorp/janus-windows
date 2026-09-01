import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src" / "ReinstallationCenter.cs").read_text(encoding="utf-8")
MAIN = (ROOT / "src" / "MigradorSeguro.cs").read_text(encoding="utf-8")
BUILD = (ROOT / "build-native.ps1").read_text(encoding="utf-8")


class ReinstallationCenterTests(unittest.TestCase):
    def test_center_is_built_and_reachable(self):
        self.assertIn("ReinstallationCenter.cs", BUILD)
        self.assertIn("new ReinstallationCenterForm(true)", MAIN)
        for section in ("Preparación", "Puesta a punto", "Perfiles", "Licencias", "Respaldos"):
            self.assertIn(f'"{section}"', SOURCE)
        self.assertIn("SelectSection", SOURCE)

    def test_main_navigation_uses_one_embedded_content_area(self):
        self.assertIn("new JanusShellForm()", MAIN)
        for section in ("Migración", "Herramientas", "Mochila", "Acerca de"):
            self.assertIn(f'AddPage(menu,"{section}"', MAIN)
        self.assertIn("form.TopLevel=false", MAIN)
        self.assertIn("form.Dock=DockStyle.Fill", MAIN)
        self.assertIn("ShowPage", MAIN)
        self.assertNotIn('new TabPage("Migración")', MAIN)

    def test_tools_no_longer_repeat_the_reinstallation_button(self):
        self.assertNotIn('Text="Mochila de reinstalación…"', MAIN)

    def test_license_check_starts_only_when_its_section_is_selected(self):
        self.assertIn("selected==3&&!licensesLoaded", SOURCE)
        self.assertNotIn("page.Enter+=async", SOURCE)

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

    def test_compact_official_download_grid_contains_requested_apps(self):
        self.assertIn("Grid(3,6,6,22)", MAIN)
        self.assertNotIn("Navegadores — descargas oficiales", MAIN)
        for app, url in (
            ("WinDirStat", "https://windirstat.net/download.html"),
            ("Everything", "https://www.voidtools.com/es-es/descargas/"),
            ("Autoruns", "https://learn.microsoft.com/es-es/sysinternals/downloads/autoruns"),
            ("IrfanView", "https://www.irfanview.com/main_download_engl.htm"),
            ("Sysinternals", "https://learn.microsoft.com/es-es/sysinternals/downloads/"),
            ("DropIt", "https://www.dropitproject.com/"),
        ):
            self.assertIn(app, MAIN)
            self.assertIn(url, MAIN)
        self.assertIn("PowerToys", MAIN)
        self.assertIn("https://learn.microsoft.com/es-es/windows/powertoys/install", MAIN)

    def test_teams_startup_control_is_detected_backed_up_and_reversible(self):
        self.assertIn("TeamsTfwStartupTask", MAIN)
        self.assertIn("com.squirrel.Teams.Teams", MAIN)
        self.assertIn("DisableTeamsStartup", MAIN)
        self.assertIn("RestoreTeamsStartup", MAIN)
        self.assertIn("TeamsStartup", MAIN)
        self.assertIn("no borrará conversaciones ni archivos", MAIN)
        self.assertNotIn("Remove-AppxPackage", MAIN)

    def test_report_excludes_sensitive_material(self):
        self.assertIn("no contiene claves de producto, contraseñas", SOURCE)
        self.assertNotIn("DigitalProductId", SOURCE)
        self.assertNotIn("password", SOURCE.lower())

    def test_license_inspection_is_explicit_and_not_persisted(self):
        self.assertIn("OA3xOriginalProductKey", SOURCE)
        self.assertIn("vnextdiag.ps1", SOURCE)
        self.assertIn("ospp.vbs", SOURCE)
        self.assertIn("¿Consultar y mostrar la clave?", SOURCE)
        self.assertIn("Las claves nunca se agregan a informes, historial ni a la Mochila completa", SOURCE)
        self.assertNotIn('Record("Licencias"', SOURCE)

    def test_stable_3_0_1_version_is_visible(self):
        self.assertIn('DisplayVersion="3.1.0-rc.1"', MAIN)
        self.assertIn('AssemblyVersion("3.1.0.0")', MAIN)
        self.assertIn('AssemblyFileVersion("3.1.0.0")', MAIN)
        self.assertIn('AssemblyInformationalVersion("3.1.0-rc.1")', MAIN)


if __name__ == "__main__":
    unittest.main()
