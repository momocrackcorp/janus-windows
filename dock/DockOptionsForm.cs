using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Diagnostics;

namespace JanusDock;

public sealed class DockOptionsForm : Form
{
    public bool AlwaysOnTop { get; private set; }
    public bool AutoHide { get; private set; }
    public bool ShowNames { get; private set; }
    public string NameMode { get; private set; } = "Tooltip";
    public string DockMode { get; private set; } = "Acoplada";
    public bool ShowDiskWidget { get; private set; }
    public bool StartWithWindows { get; private set; }
    public int DiskColorArgb { get; private set; }
    public Dictionary<string,int> DiskColors { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool HoverAnimation { get; private set; } = true;
    public bool ShowShutdown { get; private set; }
    public bool ShowRestart { get; private set; }
    public int CriticalPercent { get; private set; }
    public int IconSize { get; private set; }
    public string IconStyle { get; private set; } = "Windows";
    public string Edge { get; private set; } = "Derecha";
    const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "JANUS Dock";

    public DockOptionsForm(DockSettings config)
    {
        Text = "Opciones de JANUS Dock";
        ClientSize = new Size(570, 440);
        MinimumSize = new Size(590, 480);
        AutoScaleMode = AutoScaleMode.Dpi;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };
        Controls.Add(tabs);

        var general = new TabPage("General");
        var appearance = new TabPage("Apariencia");
        var disks = new TabPage("Discos");
        var about = new TabPage("Acerca de");
        tabs.TabPages.AddRange(new[] { general, appearance, disks, about });
        appearance.Controls.Add(new Label { Text = "Estilo de los iconos:", Location = new Point(22, 32), AutoSize = true });
        var iconStyle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(175, 28), Width = 260 };
        iconStyle.Items.AddRange(new object[] { "Iconos de Windows", "Iconos Lucide" });
        iconStyle.SelectedIndex = config.IconStyle == "Lucide" ? 1 : 0;
        appearance.Controls.Add(iconStyle);
        appearance.Controls.Add(new Label { Text = "Windows: usa los iconos originales del sistema y de los accesos.", Location = new Point(22, 83), AutoSize = true });
        appearance.Controls.Add(new Label { Text = "Lucide: estilo de líneas para Mi PC, Documentos, Descargas,", Location = new Point(22, 113), AutoSize = true });
        appearance.Controls.Add(new Label { Text = "carpetas, Apagar y Reiniciar. Los programas conservan su icono.", Location = new Point(22, 135), AutoSize = true });
        appearance.Controls.Add(new Label { Text = "El cambio se aplica al pulsar Guardar cambios.", Location = new Point(22, 186), AutoSize = true });

        var top = new CheckBox { Text = "Mantener siempre visible (encima de otras ventanas)", Checked = config.AlwaysOnTop, AutoSize = true, Location = new Point(18, 22) };
        var hide = new CheckBox { Text = "Ocultar automáticamente al alejar el puntero", Checked = config.AutoHide, AutoSize = true, Location = new Point(18, 54) };
        var startup = new CheckBox { Text = "Cargar JANUS Dock al iniciar Windows", Checked = IsStartupEnabled(), AutoSize = true, Location = new Point(18, 86) };
        var names = new Label { Text = "Nombres de accesos:", AutoSize = true, Location = new Point(18, 115) };
        var nameMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(190, 110), Width = 205 };
        nameMode.Items.AddRange(new object[] { "Solo iconos", "Al pasar el mouse", "Iconos con nombres" });
        nameMode.SelectedIndex = config.NameMode switch { "None" => 0, "Written" => 2, _ => config.ShowNames ? 2 : 1 };
        general.Controls.AddRange(new Control[] { top, hide, startup, names, nameMode });
        general.Controls.Add(new Label { Text = "Modo:", Location = new Point(18, 151), AutoSize = true });
        var dockMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(190, 147), Width = 205 };
        dockMode.Items.AddRange(new object[] { "Acoplada", "Flotante" });
        dockMode.SelectedItem = config.DockMode;
        if (dockMode.SelectedIndex < 0) dockMode.SelectedIndex = 0;
        general.Controls.Add(dockMode);
        general.Controls.Add(new Label { Text = "Ubicación:", Location = new Point(18, 190), AutoSize = true });
        var location = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(190, 186), Width = 180 };
        location.Items.AddRange(new object[] { "Derecha", "Izquierda", "Arriba", "Abajo" });
        location.SelectedItem = config.Edge;
        if (location.SelectedIndex < 0) location.SelectedIndex = 0;
        general.Controls.Add(location);
        general.Controls.Add(new Label { Text = "Tamaño iconos:", Location = new Point(18, 231), AutoSize = true });
        var size = new NumericUpDown { Location = new Point(190, 227), Minimum = 16, Maximum = 64, Increment = 8, Value = Math.Clamp(config.IconSize, 16, 64), Width = 100 };
        general.Controls.Add(size);
        var animation = new CheckBox { Text = "Animar iconos al pasar el mouse", AutoSize = true, Location = new Point(18, 263), Checked = config.HoverAnimation };
        general.Controls.Add(animation);
        var smallIcons = new CheckBox { Text = "Iconos pequeños (24 px)", AutoSize = true, Location = new Point(310, 230), Checked = config.IconSize <= 24 };
        general.Controls.Add(smallIcons);
        smallIcons.CheckedChanged += (_, _) => { if (smallIcons.Checked) size.Value = 24; else if (size.Value == 24) size.Value = 48; };
        // Explicit quick preset; manual numeric sizes remain independent.
        general.Controls.Add(new Label { Text = "Nota: al ocultarse, queda una franja para recuperar la barra con el mouse.", Location = new Point(18, 347), AutoSize = true });

        var powerTitle = new Label { Text = "Botones de energía en la barra:", AutoSize = true, Location = new Point(18, 292) };
        var shutdown = new CheckBox { Text = "Apagar", AutoSize = true, Location = new Point(240, 292), Checked = config.ShowShutdown };
        var restart = new CheckBox { Text = "Reiniciar", AutoSize = true, Location = new Point(335, 292), Checked = config.ShowRestart };
        general.Controls.AddRange(new Control[] { powerTitle, shutdown, restart });
        var widget = new CheckBox { Text = "Mostrar indicador de uso de discos en la barra", Checked = config.ShowDiskWidget, Location = new Point(18, 20), AutoSize = true };
        var diskColors = new Dictionary<string,int>(config.DiskColors, StringComparer.OrdinalIgnoreCase);
        var widgetLabel = new Label { Text = "Cada unidad tiene su propio color. Selecciónalo con el botón de su fila.", Location = new Point(18, 54), AutoSize = true };
        disks.Controls.Add(widget);
        disks.Controls.Add(widgetLabel);
        disks.Controls.Add(new Label { Text = "Avisar desde (%):", Location = new Point(18, 88), AutoSize = true });
        var critical = new NumericUpDown { Location = new Point(150, 84), Width = 80, Minimum = 70, Maximum = 99, Value = Math.Clamp(config.CriticalPercent, 70, 99) };
        disks.Controls.Add(critical);
        var diskPanel = new FlowLayoutPanel { Location = new Point(18, 124), Size = new Size(510, 175), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(2) };
        disks.Controls.Add(diskPanel);
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.TotalSize <= 0) continue;
                string letter = drive.Name.Substring(0, 2).ToUpperInvariant();
                Color color = Color.FromArgb(diskColors.TryGetValue(letter, out int existing) ? existing : config.DiskColorArgb);
                var line = new Panel { Width = 476, Height = 69 };
                var preview = new DiskUsageControl(drive, color, (int)critical.Value, false) { Location = new Point(0, 0), Size = new Size(335, 65), ForeColor = SystemColors.ControlText };
                var choose = new Button { Text = "Color...", Location = new Point(355, 16), Size = new Size(95, 30), BackColor = color, UseVisualStyleBackColor = false };
                choose.Click += (_, _) =>
                {
                    using var picker = new ColorDialog { Color = choose.BackColor, FullOpen = true };
                    if (picker.ShowDialog(this) != DialogResult.OK) return;
                    diskColors[letter] = picker.Color.ToArgb();
                    choose.BackColor = picker.Color;
                    preview.SetAppearance(picker.Color, (int)critical.Value);
                };
                critical.ValueChanged += (_, _) => preview.SetAppearance(choose.BackColor, (int)critical.Value);
                line.Controls.Add(preview); line.Controls.Add(choose);
                diskPanel.Controls.Add(line);
            }
            catch { }
        }

        var info = new Label { Text = "JANUS Dock (LNK) — v5.0 (candidata)\n\nBarra de accesos rápidos para Windows 11.\nDesarrollada en C# con Windows Forms y .NET.\n\nIdeada en Pueblo Seco, Ñuble, Chile.\n16 de octubre de 2026.\n\nAutoría: Omar Aguila\nLaboratorios Momocrackcorp", Location = new Point(104, 20), Size = new Size(420, 215) };
        var appIcon = new PictureBox { Location = new Point(20, 25), Size = new Size(64, 64), SizeMode = PictureBoxSizeMode.Zoom };
        try { using var ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath); appIcon.Image = ico?.ToBitmap() ?? SystemIcons.Application.ToBitmap(); }
        catch { appIcon.Image = SystemIcons.Application.ToBitmap(); }
        about.Controls.Add(appIcon);
        about.Controls.Add(info);
        var updates = new Button { Text = "Buscar actualizaciones en GitHub", Location = new Point(20, 235), Size = new Size(260, 35) };
        updates.Click += (_, _) => Process.Start(new ProcessStartInfo("https://github.com/momocrackcorp/janus-windows/actions/workflows/build-janus-dock.yml") { UseShellExecute = true });
        about.Controls.Add(updates);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(8) };
        Controls.Add(bar); bar.BringToFront();
        var cancel = new Button { Text = "Cancelar", Width = 110, Height = 32, DialogResult = DialogResult.Cancel };
        var apply = new Button { Text = "Guardar cambios", Width = 135, Height = 32, DialogResult = DialogResult.OK };
        bar.Controls.AddRange(new Control[] { apply, cancel });
        AcceptButton = apply; CancelButton = cancel;
        apply.Click += (_, _) =>
        {
            AlwaysOnTop = top.Checked;
            AutoHide = hide.Checked;
            StartWithWindows = startup.Checked;
            NameMode = nameMode.SelectedIndex switch { 0 => "None", 2 => "Written", _ => "Tooltip" };
            ShowNames = NameMode == "Written";
            DockMode = dockMode.SelectedItem?.ToString() ?? "Acoplada";
            ShowDiskWidget = widget.Checked;
            IconSize = (int)size.Value;
            IconStyle = iconStyle.SelectedIndex == 1 ? "Lucide" : "Windows";
            Edge = (string)location.SelectedItem!;
            DiskColorArgb = config.DiskColorArgb;
            DiskColors = diskColors;
            HoverAnimation = animation.Checked;
            ShowShutdown = shutdown.Checked;
            ShowRestart = restart.Checked;
            CriticalPercent = (int)critical.Value;
        };
    }
    public static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunPath);
        return key?.GetValue(RunValue) != null;
    }
    public static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunPath, true);
        if (enabled) key?.SetValue(RunValue, $"\"{Application.ExecutablePath}\"");
        else key?.DeleteValue(RunValue, false);
    }
}

public sealed class DiskUsageControl : Control
{
    readonly string name;
    readonly double ratio;
    readonly string details;
    readonly bool compact;
    Color accent;
    int threshold;
    public DiskUsageControl(DriveInfo drive, Color color, int criticalPercent, bool compactMode = false)
    {
        name = drive.Name.TrimEnd('\\');
        ratio = Math.Clamp((double)(drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize, 0, 1);
        details = $"{(drive.TotalSize - drive.TotalFreeSpace) / 1073741824.0:F1} GB usados · {drive.TotalFreeSpace / 1073741824.0:F1} GB libres";
        accent = color;
        threshold = criticalPercent;
        compact = compactMode;
        DoubleBuffered = true;
    }
    public void SetAppearance(Color color, int criticalPercent) { accent = color; threshold = criticalPercent; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        int ringX = compact ? Math.Max(9, (Width - 48) / 2) : 9;
        var rect = new Rectangle(ringX, 9, 46, 46);
        using var back = new Pen(Color.FromArgb(190, 197, 207), 8);
        using var front = new Pen(accent, 8);
        e.Graphics.DrawEllipse(back, rect);
        e.Graphics.DrawArc(front, rect, -90, (float)(ratio * 360));
        using var title = new Font("Segoe UI", 10, FontStyle.Bold);
        using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var textBrush = new SolidBrush(ForeColor);
        e.Graphics.DrawString(name, title, textBrush, new RectangleF(ringX + 3, 14, 42, 38), centered);
        if (ratio * 100 >= threshold)
        {
            using var warning = new SolidBrush(Color.OrangeRed);
            e.Graphics.FillEllipse(warning, ringX + 39, 5, 15, 15);
            using var symbol = new Font("Segoe UI", 9, FontStyle.Bold);
            e.Graphics.DrawString("!", symbol, Brushes.White, new RectangleF(ringX + 39, 4, 15, 16), centered);
        }
        if (!compact)
        {
            using var sub = new Font("Segoe UI", 9);
            e.Graphics.DrawString(details, sub, textBrush, 80, 28);
        }
    }
}
