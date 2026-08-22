import json
import struct
import unittest
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src" / "DualityThemeForm.cs").read_text(encoding="utf-8")
MAIN = (ROOT / "src" / "MigradorSeguro.cs").read_text(encoding="utf-8")
BUILD = (ROOT / "build-native.ps1").read_text(encoding="utf-8")
PACK = ROOT / "assets" / "theme-packs" / "duality"
CRUX = ROOT / "assets" / "windows-themes" / "crux"
NEWAITA = ROOT / "assets" / "windows-themes" / "newaita"
PAPIRUS = ROOT / "assets" / "windows-themes" / "papirus"
WHITESUR = ROOT / "assets" / "windows-themes" / "whitesur"
LACAPITAINE = ROOT / "assets" / "windows-themes" / "lacapitaine"
RETRO = ROOT / "assets" / "windows-themes" / "retro"


def external_payload_available(theme):
    """Los recursos completos se descargan desde Releases, no desde Git."""
    return (theme / "DesktopBackground").is_dir()


class DualityThemeTests(unittest.TestCase):
    def test_duality_is_an_embedded_navigation_page(self):
        self.assertIn('AddPage(menu,"Temas",new DualityThemeForm(true)', MAIN)
        self.assertIn("DualityThemeForm.cs", BUILD)
        self.assertIn("form.TopLevel=false", MAIN)

    def test_icon_catalog_is_integrated_in_themes(self):
        for name in ("JANUS Fluent Soft 3D", "Crux", "Newaita", "Papirus", "WhiteSur", "La Capitaine", "Retro"):
            self.assertIn(f'Name="{name}"', SOURCE)
        self.assertIn("iconThemeSelector.Items.AddRange(iconThemes)", SOURCE)
        self.assertIn("SelectedIconTheme.DownloadUrl", SOURCE)
        self.assertNotIn('ToolButton("Tema JANUS…"', MAIN)

    def test_icon_packages_have_an_integrated_non_destructive_preview(self):
        for text in (
            'iconPreviewButton.Text="Vista previa"',
            'iconPreviewButton.Text="Volver al fondo"',
            "void ShowIconPreview()",
            "void ShowWallpaperPreview()",
            "void LoadIconPreview()",
            "CreateIconPreviewCard",
            "Mostrarlo aquí no modifica ningún icono de Windows",
        ):
            self.assertIn(text, SOURCE)
        for icon in (
            "este-equipo", "archivos-usuario", "red", "papelera-vacia",
            "papelera-llena", "escritorio", "documentos", "descargas",
            "imagenes", "musica", "videos", "hdd-ssd", "usb", "unidad-red",
        ):
            self.assertIn(f'Tuple.Create("{icon}"', SOURCE)
        self.assertIn("RefreshState();ShowIconPreview();", SOURCE)

    def test_theme_side_panel_uses_aligned_layout_grids(self):
        self.assertIn("var packageGrid=new TableLayoutPanel", SOURCE)
        self.assertIn("var iconActions=new TableLayoutPanel", SOURCE)
        self.assertIn("iconActions.ColumnStyles.Add", SOURCE)
        self.assertIn("packageGrid.SetColumnSpan(iconThemeSelector,2)", SOURCE)
        self.assertIn("packageGrid.SetColumnSpan(iconStatus,2)", SOURCE)
        self.assertIn("soundPreview.Anchor=AnchorStyles.None", SOURCE)
        self.assertIn("soundBox.Resize+=(s,e)=>centerSound()", SOURCE)

    def test_desktop_visibility_controls_are_at_the_bottom_of_themes(self):
        self.assertIn("WindowsToolsForm.CreateDesktopPersonalizationGroup()", SOURCE)
        self.assertIn("root.Controls.Add(desktopPersonalization,0,2)", SOURCE)
        self.assertIn('Text="Iconos del Escritorio y menú Inicio"', MAIN)
        self.assertIn('ToolButton("Aplicar visibilidad"', MAIN)
        self.assertIn("desktopGrid.SetRowSpan(applyDesktopIcons,2)", MAIN)
        self.assertIn("AutoScroll=true", SOURCE)

    def test_theme_page_is_compact_enough_to_show_footer_on_entry(self):
        self.assertIn("AutoScaleMode=AutoScaleMode.None", SOURCE)
        self.assertIn("Height=810,MinimumSize=new Size(940,810)", SOURCE)
        self.assertIn("Dock=DockStyle.Top,Height=260", SOURCE)
        self.assertIn("SizeType.Absolute,165", SOURCE)
        self.assertIn("SizeType.Absolute,68", SOURCE)
        self.assertIn("applyDesktopIcons.Size=new Size(238,34)", MAIN)
        self.assertIn('Text="Equipo",AutoSize=true,Anchor=AnchorStyles.Left', MAIN)

    def test_icon_application_covers_real_known_folders_and_both_drive_locations(self):
        for folder_id in (
            "FDD39AD0-238F-46AF-ADB4-6C85480369C7",
            "374DE290-123F-4565-9164-39C4925E467B",
            "B4BFCC3A-DB2C-424C-B029-7FE99A87C641",
            "33E28130-4E1E-4676-835A-98395C3BC3BB",
            "4BD8D571-6D19-48D3-BE97-422220080E43",
            "18989B1D-99B5-455B-841C-AB7C74E4DDFC",
        ):
            self.assertIn(folder_id, SOURCE)
        self.assertIn("DriveIconKeys", SOURCE)
        self.assertIn("Software\\Classes\\Applications\\Explorer.exe\\Drives", SOURCE)
        self.assertIn("SetIconValue(quickAccessId", SOURCE)

    def test_footer_buttons_use_a_non_clipping_layout(self):
        self.assertIn("var footer=new TableLayoutPanel", SOURCE)
        self.assertIn("BackColor=Color.White,Margin=new Padding(0)", SOURCE)
        self.assertIn("var footerButtons=new FlowLayoutPanel", SOURCE)
        self.assertIn("footer.Controls.Add(safetyNote,0,0)", SOURCE)
        self.assertIn("footer.SetColumnSpan(safetyNote,3)", SOURCE)
        self.assertIn("TextAlign=ContentAlignment.MiddleCenter", SOURCE)
        self.assertIn("footer.Controls.Add(footerButtons,1,1)", SOURCE)
        self.assertIn("var footerButtons=new FlowLayoutPanel{Dock=DockStyle.Fill,Margin=new Padding(0)", SOURCE)
        self.assertIn('apply.Size=new Size(168,34)', SOURCE)
        self.assertIn('restore.Size=new Size(168,34)', SOURCE)
        self.assertIn('restore.Margin=new Padding(0)', SOURCE)

    def test_only_preview_images_are_embedded(self):
        self.assertIn("DualityClaroPreview.png", BUILD)
        self.assertIn("DualityOscuroPreview.png", BUILD)
        self.assertIn("CruxClaroPreview.png", BUILD)
        self.assertIn("CruxOscuroPreview.png", BUILD)
        for variant in ("Nublado", "Noche", "Manana"):
            self.assertIn(f"Newaita{variant}Preview.png", BUILD)
        for variant in ("Delta", "Piramides", "Jeroglificos"):
            self.assertIn(f"Papirus{variant}Preview.png", BUILD)
        for variant in ("Costa", "Boracay", "KohPoda"):
            self.assertIn(f"WhiteSur{variant}Preview.png", BUILD)
        for variant in ("Muro", "Rio", "Invierno"):
            self.assertIn(f"LaCapitaine{variant}Preview.png", BUILD)
        for variant in ("Pradera", "Puma", "Mosaico98"):
            self.assertIn(f"Retro{variant}Preview.png", BUILD)
        for folder in ("DesktopBackground", "Cursors", "Sounds", "Icons"):
            self.assertNotIn(f"theme-packs\\duality\\{folder}\\*", BUILD)
            self.assertNotIn(f"windows-themes\\crux\\{folder}\\*", BUILD)
            self.assertNotIn(f"windows-themes\\newaita\\{folder}\\*", BUILD)
            self.assertNotIn(f"windows-themes\\papirus\\{folder}\\*", BUILD)
            self.assertNotIn(f"windows-themes\\whitesur\\{folder}\\*", BUILD)
            self.assertNotIn(f"windows-themes\\lacapitaine\\{folder}\\*", BUILD)
            self.assertNotIn(f"windows-themes\\retro\\{folder}\\*", BUILD)
        self.assertNotIn("JANUS-Crux-v1.zip", BUILD)
        self.assertNotIn("JANUS-Newaita-v1.zip", BUILD)
        self.assertNotIn("JANUS-Papirus-v1.zip", BUILD)
        self.assertNotIn("JANUS-WhiteSur-v1.zip", BUILD)
        self.assertNotIn("JANUS-LaCapitaine-v1.zip", BUILD)
        self.assertNotIn("JANUS-Retro-v1.zip", BUILD)

    def test_components_are_individually_selectable(self):
        for label in (
            "Fondo adaptado a la pantalla",
            "Modo y color de énfasis combinado",
            "Cursores Duality .cur / .ani",
            "Sonidos discretos (opcional)",
            "Iconos JANUS para sistema y carpetas",
        ):
            self.assertIn(label, SOURCE)
        self.assertIn('ConfigureChoice(sounds,"Sonidos discretos (opcional)",false)', SOURCE)

    def test_apply_has_review_backup_and_automatic_rollback(self):
        self.assertIn('"Revisar "+themeName', SOURCE)
        self.assertIn("tema-anterior.json", SOURCE)
        self.assertIn("antes-de-aplicar-", SOURCE)
        self.assertIn("if(state!=null)try{RestoreState(state);}", SOURCE)
        self.assertIn("Restaurar tema anterior", SOURCE)
        self.assertIn("No se borrarán archivos personales", SOURCE)

    def test_crux_is_selectable_and_uses_its_external_package(self):
        for value in (
            'Id="crux"', 'Name="JANUS Crux"',
            'PackageFile="JANUS-Crux-v1.zip"', 'ManifestId="janus-crux"',
            'Prefix="crux"', 'Color.FromArgb(232,120,23)',
        ):
            self.assertIn(value, SOURCE)
        self.assertIn("windowsThemeSelector.Items.AddRange(windowsThemes)", SOURCE)
        self.assertIn("theme.LightAccent", SOURCE)
        self.assertIn("theme.DarkAccent", SOURCE)
        self.assertIn('prefix+"-arrow.cur"', SOURCE)
        self.assertIn('prefix+"-notify.wav"', SOURCE)
        for value in ('Wallpapers=new[]{"Claro","Oscuro"}', 'Slideshow=true', 'PreserveMode=true'):
            self.assertIn(value, SOURCE)

    def test_zip_loading_rejects_path_traversal_and_validates_files(self):
        self.assertIn("StartsWith(safeRoot,StringComparison.OrdinalIgnoreCase)", SOURCE)
        self.assertIn("El ZIP contiene una ruta no segura", SOURCE)
        self.assertIn("ValidateThemeDirectory(staging,true)", SOURCE)
        self.assertIn("ValidateIconDirectory(staging,true)", SOURCE)

    def test_manifest_declares_safe_reversible_external_components(self):
        manifest = json.loads((PACK / "manifest.json").read_text(encoding="utf-8-sig"))
        self.assertEqual(manifest["id"], "janus-duality")
        self.assertEqual(manifest["wallpapers"], ["Claro", "Oscuro"])
        self.assertEqual(manifest["slideshowMinutes"], 30)
        self.assertTrue(manifest["preserveWindowsMode"])
        self.assertTrue(manifest["safe"])
        self.assertTrue(manifest["reversible"])
        self.assertEqual(manifest["iconCompanion"], "JANUS-Duality-Iconos-v1.zip")

    @unittest.skipUnless(external_payload_available(PACK), "recursos externos no incluidos en Git")
    def test_cursor_and_sound_assets_are_well_formed(self):
        cursors = list((PACK / "Cursors").glob("*.cur"))
        animations = list((PACK / "Cursors").glob("*.ani"))
        sounds = list((PACK / "Sounds").glob("*.wav"))
        self.assertEqual(len(cursors), 13)
        self.assertEqual(len(animations), 2)
        self.assertEqual(len(sounds), 6)
        for cursor in cursors:
            reserved, kind, count = struct.unpack("<HHH", cursor.read_bytes()[:6])
            self.assertEqual((reserved, kind, count), (0, 2, 1))
        for animation in animations:
            data = animation.read_bytes()
            self.assertEqual(data[:4], b"RIFF")
            self.assertEqual(data[8:12], b"ACON")
        for sound in sounds:
            data = sound.read_bytes()
            self.assertEqual(data[:4], b"RIFF")
            self.assertEqual(data[8:12], b"WAVE")

    @unittest.skipUnless(external_payload_available(PACK), "recursos externos no incluidos en Git")
    def test_icon_companion_is_based_on_the_original_janus_set(self):
        expected = {
            "este-equipo", "archivos-usuario", "red", "papelera-vacia",
            "papelera-llena", "documentos", "descargas", "escritorio",
            "imagenes", "musica", "videos", "hdd-ssd", "usb", "unidad-red",
        }
        icons = PACK / "Icons"
        self.assertEqual({p.stem for p in icons.glob("*.ico")}, expected)
        self.assertEqual({p.stem for p in icons.glob("*.png")}, expected)

    @unittest.skipUnless(external_payload_available(CRUX), "recursos externos no incluidos en Git")
    def test_crux_manifest_and_assets_are_complete(self):
        manifest = json.loads((CRUX / "manifest.json").read_text(encoding="utf-8-sig"))
        self.assertEqual(manifest["id"], "janus-crux")
        self.assertEqual(manifest["accentColor"], "#E87817")
        self.assertEqual(manifest["wallpapers"], ["Claro", "Oscuro"])
        self.assertEqual(manifest["slideshowMinutes"], 30)
        self.assertTrue(manifest["preserveWindowsMode"])
        self.assertTrue(manifest["safe"])
        self.assertTrue(manifest["reversible"])
        self.assertEqual(len(list((CRUX / "Cursors").glob("*.cur"))), 13)
        self.assertEqual(len(list((CRUX / "Cursors").glob("*.ani"))), 2)
        self.assertEqual(len(list((CRUX / "Sounds").glob("*.wav"))), 6)
        for name in (
            "JANUS-Crux-Claro-4K.png", "JANUS-Crux-Claro-Ultrawide-5K.png",
            "JANUS-Crux-Oscuro-4K.png", "JANUS-Crux-Oscuro-Ultrawide-5K.png",
        ):
            self.assertTrue((CRUX / "DesktopBackground" / name).is_file(), name)

    def test_newaita_has_three_wallpapers_and_a_distinct_external_package(self):
        for value in (
            'Id="newaita"', 'Name="JANUS Newaita"',
            'PackageFile="JANUS-Newaita-v1.zip"', 'ManifestId="janus-newaita"',
            'Prefix="newaita"', 'Variants=new[]{"Nublado","Noche","Manana"}',
            'Wallpapers=new[]{"Nublado","Noche","Manana"}',
            'Slideshow=true', 'PreserveMode=true', 'Color.FromArgb(91,141,175)',
        ):
            self.assertIn(value, SOURCE)
        self.assertIn("thirdVariant", SOURCE)
        self.assertIn('if(token=="Manana")return "Mañana"', SOURCE)

    @unittest.skipUnless(external_payload_available(NEWAITA), "recursos externos no incluidos en Git")
    def test_newaita_assets_and_session_sounds_are_complete(self):
        manifest = json.loads((NEWAITA / "manifest.json").read_text(encoding="utf-8-sig"))
        self.assertEqual(manifest["id"], "janus-newaita")
        self.assertEqual(manifest["accentColor"], "#5B8DAF")
        self.assertEqual(manifest["variants"], ["Nublado", "Noche", "Manana"])
        self.assertEqual(manifest["wallpapers"], ["Nublado", "Noche", "Manana"])
        self.assertEqual(manifest["slideshowMinutes"], 30)
        self.assertTrue(manifest["preserveWindowsMode"])
        self.assertTrue(manifest["safe"])
        self.assertTrue(manifest["reversible"])
        self.assertEqual(len(list((NEWAITA / "Cursors").glob("*.cur"))), 13)
        self.assertEqual(len(list((NEWAITA / "Cursors").glob("*.ani"))), 2)
        self.assertEqual(len(list((NEWAITA / "Sounds").glob("*.wav"))), 9)
        for variant in ("Nublado", "Noche", "Manana"):
            for suffix in ("-4K.jpg", "-Ultrawide-5K.jpg"):
                self.assertTrue((NEWAITA / "DesktopBackground" / f"JANUS-Newaita-{variant}{suffix}").is_file())

    def test_newaita_session_sounds_use_only_reversible_windows_events(self):
        for event in ("SystemStart", "WindowsLogon", "WindowsLogoff", "SystemExit"):
            self.assertIn(f'events["{event}"]', SOURCE)
            self.assertIn(f'"{event}"', SOURCE)
        self.assertNotIn("TaskScheduler", SOURCE)
        self.assertNotIn("schtasks", SOURCE.lower())

    def test_papirus_has_three_egyptian_wallpapers_and_its_own_palette(self):
        for value in (
            'Id="papirus"', 'Name="JANUS Papirus"',
            'PackageFile="JANUS-Papirus-v1.zip"', 'ManifestId="janus-papirus"',
            'Prefix="papirus"', 'Variants=new[]{"Delta","Piramides","Jeroglificos"}',
            'Wallpapers=new[]{"Delta","Piramides","Jeroglificos"}',
            'Slideshow=true', 'PreserveMode=true',
            'Color.FromArgb(38,166,91)', 'Color.FromArgb(166,83,42)',
        ):
            self.assertIn(value, SOURCE)
        self.assertIn('token=="Piramides"', SOURCE)
        self.assertIn('token=="Jeroglificos"', SOURCE)

    @unittest.skipUnless(external_payload_available(PAPIRUS), "recursos externos no incluidos en Git")
    def test_papirus_assets_and_external_packages_are_complete(self):
        manifest = json.loads((PAPIRUS / "manifest.json").read_text(encoding="utf-8-sig"))
        self.assertEqual(manifest["id"], "janus-papirus")
        self.assertEqual(manifest["accentColor"], "#26A65B")
        self.assertEqual(manifest["secondaryAccentColor"], "#A6532A")
        self.assertIn("Egyptian-inspired", manifest["soundStyle"])
        self.assertEqual(manifest["variants"], ["Delta", "Piramides", "Jeroglificos"])
        self.assertEqual(manifest["wallpapers"], ["Delta", "Piramides", "Jeroglificos"])
        self.assertEqual(manifest["slideshowMinutes"], 30)
        self.assertTrue(manifest["preserveWindowsMode"])
        self.assertTrue(manifest["safe"])
        self.assertTrue(manifest["reversible"])
        self.assertEqual(len(list((PAPIRUS / "Cursors").glob("*.cur"))), 13)
        self.assertEqual(len(list((PAPIRUS / "Cursors").glob("*.ani"))), 2)
        self.assertEqual(len(list((PAPIRUS / "Sounds").glob("*.wav"))), 9)
        for variant in ("Delta", "Piramides", "Jeroglificos"):
            for suffix in ("-4K.jpg", "-Ultrawide-5K.jpg"):
                self.assertTrue((PAPIRUS / "DesktopBackground" / f"JANUS-Papirus-{variant}{suffix}").is_file())
            self.assertTrue((PAPIRUS / "Preview" / f"JANUS-Papirus-{variant}-preview.png").is_file())
        self.assertTrue((ROOT / "dist" / "JANUS-Papirus-v1.zip").is_file())
        self.assertTrue((ROOT / "dist" / "JANUS-Papirus-Iconos-v1.zip").is_file())

    def test_papirus_sounds_use_original_egyptian_inspired_timbres(self):
        script = (ROOT / "tools" / "build-papirus-assets.ps1").read_text(encoding="utf-8")
        for component in ("$reed", "$oud", "$breath", "$frameDrum"):
            self.assertIn(component, script)
        for hijaz_note in ("293.66", "311.13", "369.99", "392.00"):
            self.assertIn(hijaz_note, script)

    def test_whitesur_is_external_and_uses_the_requested_windows_palette(self):
        for value in (
            'Id="whitesur"', 'Name="JANUS WhiteSur"',
            'PackageFile="JANUS-WhiteSur-v1.zip"', 'ManifestId="janus-whitesur"',
            'Prefix="whitesur"', 'Variants=new[]{"Costa","Boracay","KohPoda"}',
            'Wallpapers=new[]{"Costa","Boracay","KohLipe","Perhentian","Guam","Florida","KohPoda"}',
            'BaseColor=Color.FromArgb(100,124,100)', 'LightAccent=Color.FromArgb(0,183,195)',
            'Slideshow=true',
        ):
            self.assertIn(value, SOURCE)

    @unittest.skipUnless(external_payload_available(WHITESUR), "recursos externos no incluidos en Git")
    def test_whitesur_assets_slideshow_and_licenses_are_complete(self):
        manifest = json.loads((WHITESUR / "manifest.json").read_text(encoding="utf-8-sig"))
        variants = ["Costa", "Boracay", "KohLipe", "Perhentian", "Guam", "Florida", "KohPoda"]
        self.assertEqual(manifest["id"], "janus-whitesur")
        self.assertEqual(manifest["baseColor"], "#647C64")
        self.assertEqual(manifest["accentColor"], "#00B7C3")
        self.assertEqual(manifest["variants"], variants)
        self.assertEqual(manifest["slideshowIntervalMilliseconds"], 1800000)
        self.assertTrue(manifest["shuffle"])
        self.assertTrue(manifest["safe"])
        self.assertTrue(manifest["reversible"])
        self.assertEqual(len(list((WHITESUR / "Cursors").glob("*.cur"))), 13)
        self.assertEqual(len(list((WHITESUR / "Cursors").glob("*.ani"))), 2)
        self.assertEqual(len(list((WHITESUR / "Sounds").glob("*.wav"))), 9)
        for variant in variants:
            for suffix in ("-4K.jpg", "-Ultrawide-5K.jpg"):
                self.assertTrue((WHITESUR / "DesktopBackground" / f"JANUS-WhiteSur-{variant}{suffix}").is_file())
        for preview in ("Costa", "Boracay", "KohPoda"):
            self.assertTrue((WHITESUR / "Preview" / f"JANUS-WhiteSur-{preview}-preview.png").is_file())
        credits = (WHITESUR / "CREDITOS-FONDOS.txt").read_text(encoding="utf-8-sig")
        for license_name in ("CC0 1.0", "CC BY 4.0", "dominio público"):
            self.assertIn(license_name, credits)
        self.assertTrue((ROOT / "dist" / "themes" / "JANUS-WhiteSur-v1.zip").is_file())
        self.assertTrue((ROOT / "dist" / "themes" / "Tema-Iconos-WhiteSur.zip").is_file())
        with zipfile.ZipFile(ROOT / "dist" / "themes" / "JANUS-WhiteSur-v1.zip") as package:
            names = package.namelist()
            self.assertFalse(any("Sources/" in name or "-source." in name for name in names))
            self.assertFalse(any(name.startswith("Icons/") for name in names))
            self.assertIn("CREDITOS-FONDOS.txt", names)
        for cursor in (WHITESUR / "Cursors").glob("*.cur"):
            self.assertEqual(struct.unpack("<HHH", cursor.read_bytes()[:6]), (0, 2, 1))
        for animation in (WHITESUR / "Cursors").glob("*.ani"):
            data = animation.read_bytes()
            self.assertEqual(data[:4], b"RIFF")
            self.assertEqual(data[8:12], b"ACON")
        for sound in (WHITESUR / "Sounds").glob("*.wav"):
            data = sound.read_bytes()
            self.assertEqual(data[:4], b"RIFF")
            self.assertEqual(data[8:12], b"WAVE")

    def test_whitesur_uses_native_windows_slideshow_and_original_aquatic_audio(self):
        script = (ROOT / "tools" / "build-whitesur-assets.ps1").read_text(encoding="utf-8")
        for component in ("$wave", "$bubble", "$chime", "$foam"):
            self.assertIn(component, script)
        for native_api in ("IDesktopWallpaper", "SetSlideshow(items)", "SetSlideshowOptions(1,1800000)"):
            self.assertIn(native_api, SOURCE)
        for theme_setting in ('"[Slideshow]"', '"Interval=1800000"', '"Shuffle=1"'):
            self.assertIn(theme_setting, SOURCE)

    def test_lacapitaine_is_external_and_uses_the_approved_mountain_palette(self):
        for value in (
            'Id="lacapitaine"', 'Name="JANUS La Capitaine"',
            'PackageFile="JANUS-LaCapitaine-v1.zip"', 'ManifestId="janus-lacapitaine"',
            'Prefix="lacapitaine"', 'Variants=new[]{"Muro","Rio","Invierno"}',
            'Wallpapers=new[]{"Muro","Rio","Panorama","Invierno","Nubes"}',
            'BaseColor=Color.FromArgb(89,101,107)',
            'LightAccent=Color.FromArgb(214,76,63)',
            'DarkAccent=Color.FromArgb(47,111,137)', 'Slideshow=true',
        ):
            self.assertIn(value, SOURCE)

    @unittest.skipUnless(external_payload_available(LACAPITAINE), "recursos externos no incluidos en Git")
    def test_lacapitaine_assets_slideshow_and_licenses_are_complete(self):
        manifest = json.loads((LACAPITAINE / "manifest.json").read_text(encoding="utf-8-sig"))
        variants = ["Muro", "Rio", "Panorama", "Invierno", "Nubes"]
        self.assertEqual(manifest["id"], "janus-lacapitaine")
        self.assertEqual(manifest["baseColor"], "#59656B")
        self.assertEqual(manifest["accentColor"], "#D64C3F")
        self.assertEqual(manifest["secondaryAccentColor"], "#2F6F89")
        self.assertEqual(manifest["variants"], variants)
        self.assertEqual(manifest["slideshowIntervalMilliseconds"], 1800000)
        self.assertTrue(manifest["shuffle"])
        self.assertTrue(manifest["safe"])
        self.assertTrue(manifest["reversible"])
        self.assertEqual(len(list((LACAPITAINE / "Cursors").glob("*.cur"))), 13)
        self.assertEqual(len(list((LACAPITAINE / "Cursors").glob("*.ani"))), 2)
        self.assertEqual(len(list((LACAPITAINE / "Sounds").glob("*.wav"))), 9)
        for variant in variants:
            for suffix in ("-4K.jpg", "-Ultrawide-5K.jpg"):
                self.assertTrue((LACAPITAINE / "DesktopBackground" / f"JANUS-LaCapitaine-{variant}{suffix}").is_file())
        for preview in ("Muro", "Rio", "Invierno"):
            self.assertTrue((LACAPITAINE / "Preview" / f"JANUS-LaCapitaine-{preview}-preview.png").is_file())
        credits = (LACAPITAINE / "CREDITOS-FONDOS.txt").read_text(encoding="utf-8-sig")
        for license_name in ("CC BY-SA 4.0", "CC BY-SA 3.0", "CC BY-SA 2.0"):
            self.assertIn(license_name, credits)
        theme_zip = ROOT / "dist" / "themes" / "JANUS-LaCapitaine-v1.zip"
        icon_zip = ROOT / "dist" / "themes" / "Tema-Iconos-La-Capitaine.zip"
        self.assertTrue(theme_zip.is_file())
        self.assertTrue(icon_zip.is_file())
        with zipfile.ZipFile(theme_zip) as package:
            names = package.namelist()
            self.assertFalse(any("Sources/" in name or "-source." in name for name in names))
            self.assertFalse(any(name.startswith("Icons/") for name in names))
            self.assertIn("CREDITOS-FONDOS.txt", names)
        for cursor in (LACAPITAINE / "Cursors").glob("*.cur"):
            self.assertEqual(struct.unpack("<HHH", cursor.read_bytes()[:6]), (0, 2, 1))
        for animation in (LACAPITAINE / "Cursors").glob("*.ani"):
            data = animation.read_bytes()
            self.assertEqual(data[:4], b"RIFF")
            self.assertEqual(data[8:12], b"ACON")
        for sound in (LACAPITAINE / "Sounds").glob("*.wav"):
            data = sound.read_bytes()
            self.assertEqual(data[:4], b"RIFF")
            self.assertEqual(data[8:12], b"WAVE")

    def test_lacapitaine_uses_original_mountain_audio_and_native_slideshow(self):
        script = (ROOT / "tools" / "build-lacapitaine-assets.ps1").read_text(encoding="utf-8")
        for component in ("$wind", "$stone", "$bell", "$rope"):
            self.assertIn(component, script)
        for native_api in ("IDesktopWallpaper", "SetSlideshow(items)", "SetSlideshowOptions(1,1800000)"):
            self.assertIn(native_api, SOURCE)

    def test_retro_is_external_and_uses_a_distinct_classic_palette(self):
        for value in (
            'Id="retro"', 'Name="JANUS Retro"',
            'PackageFile="JANUS-Retro-v1.zip"', 'ManifestId="janus-retro"',
            'Prefix="retro"', 'Variants=new[]{"Pradera","Puma","Mosaico98"}',
            'Wallpapers=new[]{"Pradera","Nubes","Bosque","Puma","Mosaico98"}',
            'BaseColor=Color.FromArgb(0,128,128)',
            'LightAccent=Color.FromArgb(0,84,227)',
            'DarkAccent=Color.FromArgb(242,154,46)',
            'Slideshow=true', 'PreserveMode=true',
            'FileName="Tema-Iconos-Retro.zip"',
        ):
            self.assertIn(value, SOURCE)

    @unittest.skipUnless(external_payload_available(RETRO), "recursos externos no incluidos en Git")
    def test_retro_assets_and_external_packages_are_complete(self):
        manifest = json.loads((RETRO / "manifest.json").read_text(encoding="utf-8-sig"))
        variants = ["Pradera", "Puma", "Mosaico98"]
        wallpapers = ["Pradera", "Nubes", "Bosque", "Puma", "Mosaico98"]
        self.assertEqual(manifest["id"], "janus-retro")
        self.assertEqual(manifest["baseColor"], "#008080")
        self.assertEqual(manifest["accentColor"], "#0054E3")
        self.assertEqual(manifest["secondaryAccentColor"], "#F29A2E")
        self.assertEqual(manifest["variants"], variants)
        self.assertEqual(manifest["wallpapers"], wallpapers)
        self.assertEqual(manifest["slideshowMinutes"], 30)
        self.assertTrue(manifest["preserveWindowsMode"])
        self.assertEqual(manifest["watermarkedWallpapers"], ["Nubes", "Bosque"])
        self.assertTrue(manifest["safe"])
        self.assertTrue(manifest["reversible"])
        self.assertEqual(len(list((RETRO / "Cursors").glob("*.cur"))), 13)
        self.assertEqual(len(list((RETRO / "Cursors").glob("*.ani"))), 2)
        self.assertEqual(len(list((RETRO / "Sounds").glob("*.wav"))), 9)
        self.assertEqual(len(list((RETRO / "Icons").glob("*.ico"))), 14)
        self.assertEqual(len(list((RETRO / "Icons").glob("*.png"))), 14)
        for variant in wallpapers:
            for suffix in ("-4K.jpg", "-Ultrawide-5K.jpg"):
                self.assertTrue((RETRO / "DesktopBackground" / f"JANUS-Retro-{variant}{suffix}").is_file())
        for variant in variants:
            self.assertTrue((RETRO / "Preview" / f"JANUS-Retro-{variant}-preview.png").is_file())
        theme_zip = ROOT / "dist" / "themes" / "JANUS-Retro-v1.zip"
        icon_zip = ROOT / "dist" / "themes" / "Tema-Iconos-Retro.zip"
        self.assertTrue(theme_zip.is_file())
        self.assertTrue(icon_zip.is_file())
        with zipfile.ZipFile(theme_zip) as package:
            names = package.namelist()
            self.assertFalse(any("Sources/" in name or "-source." in name for name in names))
            self.assertFalse(any(name.startswith("Icons/") for name in names))
            self.assertIn("README.txt", names)
        for cursor in (RETRO / "Cursors").glob("*.cur"):
            self.assertEqual(struct.unpack("<HHH", cursor.read_bytes()[:6]), (0, 2, 1))
        for animation in (RETRO / "Cursors").glob("*.ani"):
            data = animation.read_bytes()
            self.assertEqual(data[:4], b"RIFF")
            self.assertEqual(data[8:12], b"ACON")
        for sound in (RETRO / "Sounds").glob("*.wav"):
            data = sound.read_bytes()
            self.assertEqual(data[:4], b"RIFF")
            self.assertEqual(data[8:12], b"WAVE")

    def test_retro_uses_a_distinct_original_1990s_pc_audio_signature(self):
        script = (ROOT / "tools" / "build-retro-assets.ps1").read_text(encoding="utf-8")
        for component in ("$squareWave", "$fmBell", "$lowPulse", "$noiseBurst"):
            self.assertIn(component, script)
        self.assertIn("sampleRate=22050", script)
        self.assertIn("geometría funcional de los escritorios 3.11", script)

    def test_applied_themes_are_registered_in_windows_personalization(self):
        self.assertIn('"Microsoft","Windows","Themes"', SOURCE)
        self.assertIn("RegisterWindowsThemes();", SOURCE)
        self.assertIn('"[Control Panel\\\\Desktop]"', SOURCE)
        self.assertIn('"[VisualStyles]"', SOURCE)
        self.assertIn('"[MasterThemeSelector]"', SOURCE)
        self.assertIn('"MTSM=DABJDKT"', SOURCE)
        self.assertIn('".theme"', SOURCE)
        self.assertNotIn('Environment.SpecialFolder.Windows', SOURCE)


if __name__ == "__main__":
    unittest.main()
