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
    public bool ShowDiskWidget { get; private set; }
    public bool StartWithWindows { get; private set; }
    public int IconSize { get; private set; }
    public string Edge { get; private set; }
    const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "JANUS Dock";

    public DockOptionsForm(DockSettings config)
    {
        Text = "Opciones de JANUS Dock";
        Size = new Size(565, 450); MinimumSize = Size;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        Controls.Add(tabs);

        var general = new TabPage("General");
        var disks = new TabPage("Discos");
        var about = new TabPage("Acerca de");
        tabs.TabPages.AddRange(new[] { general, disks, about });

        var top = new CheckBox { Text = "Mantener siempre visible (encima de otras ventanas)", Checked = config.AlwaysOnTop, AutoSize = true, Location = new Point(18, 22) };
        var hide = new CheckBox { Text = "Ocultar automáticamente al alejar el puntero", Checked = config.AutoHide, AutoSize = true, Location = new Point(18, 54) };
        var startup = new CheckBox { Text = "Cargar JANUS Dock al iniciar Windows", Checked = IsStartupEnabled(), AutoSize = true, Location = new Point(18, 86) };
        var names = new CheckBox { Text = "Mostrar nombres de los accesos", Checked = config.ShowNames, AutoSize = true, Location = new Point(18, 118) };
        general.Controls.AddRange(new Control[] { top, hide, startup, names });
        general.Controls.Add(new Label { Text = "Ubicación:", Location = new Point(18, 162), AutoSize = true });
        var location = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(120, 158), Width = 180 };
        location.Items.AddRange(new object[] { "Derecha", "Izquierda", "Arriba", "Abajo" });
        location.SelectedItem = config.Edge;
        if (location.SelectedIndex < 0) location.SelectedIndex = 0;
        general.Controls.Add(location);
        general.Controls.Add(new Label { Text = "Tamaño iconos:", Location = new Point(18, 201), AutoSize = true });
        var size = new NumericUpDown { Location = new Point(120, 198), Minimum = 24, Maximum = 64, Increment = 8, Value = Math.Clamp(config.IconSize, 24, 64), Width = 100 };
        general.Controls.Add(size);
        general.Controls.Add(new Label { Text = "Nota: al ocultarse, queda una franja para recuperar la barra con el mouse.", Location = new Point(18, 247), AutoSize = true });

        var widget = new CheckBox { Text = "Mostrar indicador de uso de discos en la barra", Checked = config.ShowDiskWidget, Location = new Point(18, 20), AutoSize = true };
        disks.Controls.Add(widget);
        disks.Controls.Add(new Label { Text = "Vista previa de capacidad (GB usados y libres):", Location = new Point(18, 58), AutoSize = true });
        var diskPanel = new FlowLayoutPanel { Location = new Point(18, 88), Size = new Size(475, 225), AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        disks.Controls.Add(diskPanel);
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.TotalSize <= 0) continue;
                diskPanel.Controls.Add(new DiskUsageControl(drive) { Width = 455, Height = 68 });
            }
            catch { }
        }

        var info = new Label { Text = "JANUS Dock\n\nBarra de acceso rápido para Windows 11.\nInspirada en las barras de herramientas clásicas de Windows.\n\nDesarrollada como complemento del proyecto JANUS.\nConfiguración y accesos personales almacenados localmente.", Location = new Point(20, 25), Size = new Size(460, 190) };
        about.Controls.Add(info);
        var updates = new Button { Text = "Buscar actualizaciones en GitHub", Location = new Point(20, 235), Size = new Size(260, 35) };
        updates.Click += (_, _) => Process.Start(new ProcessStartInfo("https://github.com/momocrackcorp/janus-windows/actions/workflows/build-janus-dock.yml") { UseShellExecute = true });
        about.Controls.Add(updates);

        var bar = new Panel { Dock = DockStyle.Bottom, Height = 52 };
        Controls.Add(bar); bar.BringToFront();
        var cancel = new Button { Text = "Cancelar", Location = new Point(341, 10), Width = 94, DialogResult = DialogResult.Cancel };
        var apply = new Button { Text = "Guardar", Location = new Point(440, 10), Width = 94, DialogResult = DialogResult.OK };
        bar.Controls.AddRange(new Control[] { cancel, apply });
        AcceptButton = apply; CancelButton = cancel;
        apply.Click += (_, _) =>
        {
            AlwaysOnTop = top.Checked;
            AutoHide = hide.Checked;
            StartWithWindows = startup.Checked;
            ShowNames = names.Checked;
            ShowDiskWidget = widget.Checked;
            IconSize = (int)size.Value;
            Edge = (string)location.SelectedItem!;
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
    public DiskUsageControl(DriveInfo drive)
    {
        name = drive.Name;
        ratio = Math.Clamp((double)(drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize, 0, 1);
        details = $"{(drive.TotalSize - drive.TotalFreeSpace) / 1073741824.0:F1} GB usados · {drive.TotalFreeSpace / 1073741824.0:F1} GB libres";
        DoubleBuffered = true;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var rect = new Rectangle(8, 8, 48, 48);
        using var back = new Pen(Color.FromArgb(190, 197, 207), 8);
        using var front = new Pen(Color.FromArgb(48, 130, 222), 8);
        e.Graphics.DrawEllipse(back, rect);
        e.Graphics.DrawArc(front, rect, -90, (float)(ratio * 360));
        using var title = new Font("Segoe UI", 10, FontStyle.Bold);
        using var sub = new Font("Segoe UI", 9);
        e.Graphics.DrawString(name, title, Brushes.Black, 80, 9);
        e.Graphics.DrawString(details, sub, Brushes.DimGray, 80, 34);
    }
}
