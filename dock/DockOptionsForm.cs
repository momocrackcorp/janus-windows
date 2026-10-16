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
    public int CriticalPercent { get; private set; }
    public int IconSize { get; private set; }
    public string Edge { get; private set; } = "Derecha";
    const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "JANUS Dock";

    public DockOptionsForm(DockSettings config)
    {
        Text = "Opciones de JANUS Dock";
        ClientSize = new Size(570, 392);
        MinimumSize = new Size(590, 430);
        AutoScaleMode = AutoScaleMode.Dpi;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };
        Controls.Add(tabs);

        var general = new TabPage("General");
        var disks = new TabPage("Discos");
        var about = new TabPage("Acerca de");
        tabs.TabPages.AddRange(new[] { general, disks, about });

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
        general.Controls.Add(new Label { Text = "Nota: al ocultarse, queda una franja para recuperar la barra con el mouse.", Location = new Point(18, 276), AutoSize = true });

        var widget = new CheckBox { Text = "Mostrar indicador de uso de discos en la barra", Checked = config.ShowDiskWidget, Location = new Point(18, 20), AutoSize = true };
        var diskColor = Color.FromArgb(config.DiskColorArgb);
        var chooseColor = new Button { Text = "Color del indicador...", Location = new Point(18, 51), Width = 165, Height = 28 };
        var swatch = new Panel { Location = new Point(191, 55), Size = new Size(25, 20), BackColor = diskColor };
        var criticalLabel = new Label { Text = "Alerta desde (%):", Location = new Point(245, 56), AutoSize = true };
        var critical = new NumericUpDown { Location = new Point(360, 52), Width = 75, Minimum = 70, Maximum = 99, Value = Math.Clamp(config.CriticalPercent, 70, 99) };
        disks.Controls.AddRange(new Control[] { chooseColor, swatch, criticalLabel, critical });
        disks.Controls.Add(widget);
        disks.Controls.Add(new Label { Text = "Vista previa de capacidad (GB usados y libres):", Location = new Point(18, 102), AutoSize = true });
        var diskPanel = new FlowLayoutPanel { Location = new Point(18, 138), Size = new Size(500, 146), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        disks.Controls.Add(diskPanel);
        chooseColor.Click += (_, _) => { using var picker = new ColorDialog { Color = diskColor, FullOpen = true }; if (picker.ShowDialog(this) == DialogResult.OK) { diskColor = picker.Color; swatch.BackColor = diskColor; foreach (var item in diskPanel.Controls.OfType<DiskUsageControl>()) item.SetAppearance(diskColor, (int)critical.Value); } };
        critical.ValueChanged += (_, _) => { foreach (var item in diskPanel.Controls.OfType<DiskUsageControl>()) item.SetAppearance(diskColor, (int)critical.Value); };
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.TotalSize <= 0) continue;
                diskPanel.Controls.Add(new DiskUsageControl(drive, diskColor, (int)critical.Value, false) { Width = 475, Height = 68 });
            }
            catch { }
        }

        var info = new Label { Text = "JANUS Dock (LNK)\n\nBarra de accesos rápidos para Windows 11.\nDesarrollada en C# con Windows Forms y .NET.\n\nIdeada en Pueblo Seco, Ñuble, Chile.\n16 de octubre de 2026.\n\nAutoría: Omar Aguila\nLaboratorios Momocrackcorp", Location = new Point(20, 20), Size = new Size(490, 215) };
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
            Edge = (string)location.SelectedItem!;
            DiskColorArgb = diskColor.ToArgb();
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
