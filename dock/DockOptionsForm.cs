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
    public int ZoomPercent { get; private set; } = 280;
    public bool ShowShutdown { get; private set; }
    public bool ShowRestart { get; private set; }
    public int CriticalPercent { get; private set; }
    public int IconSize { get; private set; }
    public int BarWidth { get; private set; }
    public int BarHeight { get; private set; }
    public string IconStyle { get; private set; } = "Windows";
    public List<DockItem> OrderedItems { get; private set; } = new();
    public string Edge { get; private set; } = "Derecha";
    const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "JANUS Dock";

    public DockOptionsForm(DockSettings config, string? initialTab = null)
    {
        Text = "Opciones de JANUS Dock";
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(860, 650);
        MinimumSize = new Size(560, 440);
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
        if (initialTab == "Apariencia") tabs.SelectedTab = appearance;
        appearance.Controls.Add(new Label { Text = "Estilo de los iconos:", Location = new Point(22, 32), AutoSize = true });
        var iconStyle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(175, 28), Width = 260 };
        iconStyle.Items.AddRange(new object[] { "Iconos de Windows", "Iconos Lucide" });
        iconStyle.SelectedIndex = config.IconStyle == "Lucide" ? 1 : 0;
        appearance.Controls.Add(iconStyle);
        appearance.Controls.Add(new Label { Text = "Windows: usa los iconos originales del sistema y de los accesos.", Location = new Point(22, 83), AutoSize = true });
        appearance.Controls.Add(new Label { Text = "Lucide: estilo de líneas para Mi PC, Documentos, Descargas,", Location = new Point(22, 113), AutoSize = true });
        appearance.Controls.Add(new Label { Text = "carpetas, Apagar y Reiniciar. Los programas conservan su icono.", Location = new Point(22, 135), AutoSize = true });
        appearance.Controls.Add(new Label { Text = "Organizar accesos y separadores", Location = new Point(22, 177), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) });
        appearance.Controls.Add(new Label { Text = "Arrastra filas para ordenarlas o utiliza los botones. Guardar aplica los cambios.", Location = new Point(22, 200), AutoSize = true });
        var ordered = config.Items.Select(i => new DockItem { Name = i.Name, Path = i.Path, IsSeparator = i.IsSeparator, IsDiskWidget = i.IsDiskWidget }).ToList();
        var history = new Stack<List<DockItem>>();
        var orderList = new ListBox { Location = new Point(22, 226), Size = new Size(365, 130), AllowDrop = true, IntegralHeight = false, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        string LabelFor(DockItem i) => i.IsSeparator ? "──────── Separador ────────" : i.IsDiskWidget ? "▣ Indicadores de discos" : i.Path == "janus:shutdown" ? "⏻ Apagar" : i.Path == "janus:restart" ? "↻ Reiniciar" : (string.IsNullOrWhiteSpace(i.Name) ? i.Path : i.Name);
        void RefreshOrder(int selected)
        {
            orderList.BeginUpdate();
            orderList.Items.Clear();
            foreach (var entry in ordered) orderList.Items.Add(LabelFor(entry));
            orderList.EndUpdate();
            if (ordered.Count > 0) orderList.SelectedIndex = Math.Clamp(selected, 0, ordered.Count - 1);
        }
        void Snapshot() => history.Push(new List<DockItem>(ordered));
        void MoveSelected(int delta)
        {
            int from = orderList.SelectedIndex, to = from + delta;
            if (from < 0 || to < 0 || to >= ordered.Count) return;
            Snapshot();
            var element = ordered[from]; ordered.RemoveAt(from); ordered.Insert(to, element);
            RefreshOrder(to);
        }
        orderList.MouseDown += (_, e) =>
        {
            int from = orderList.IndexFromPoint(e.Location);
            if (from >= 0 && from < ordered.Count)
            {
                orderList.SelectedIndex = from;
                orderList.DoDragDrop(from, DragDropEffects.Move);
            }
        };
        orderList.DragOver += (_, e) => { e.Effect = e.Data?.GetDataPresent(typeof(int)) == true ? DragDropEffects.Move : DragDropEffects.None; };
        orderList.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(typeof(int)) is not int from || from < 0 || from >= ordered.Count) return;
            var point = orderList.PointToClient(new Point(e.X, e.Y));
            int to = orderList.IndexFromPoint(point);
            if (to == ListBox.NoMatches) to = ordered.Count - 1;
            if (to == from) return;
            Snapshot();
            var element = ordered[from]; ordered.RemoveAt(from); ordered.Insert(to, element);
            RefreshOrder(to);
        };
        appearance.Controls.Add(orderList);
        var actions = new FlowLayoutPanel { Location = new Point(395, 226), Size = new Size(128, 140), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        Button Action(string label)
        {
            var button = new Button { Text = label, Width = 110, Height = 26, Margin = new Padding(1) };
            actions.Controls.Add(button);
            return button;
        }
        Action("↑ Subir").Click += (_, _) => MoveSelected(-1);
        Action("↓ Bajar").Click += (_, _) => MoveSelected(1);
        Action("+ Separador").Click += (_, _) =>
        {
            Snapshot();
            int index = Math.Clamp(orderList.SelectedIndex + 1, 0, ordered.Count);
            ordered.Insert(index, new DockItem { Name = "Separador", IsSeparator = true });
            RefreshOrder(index);
        };
        Action("Eliminar").Click += (_, _) =>
        {
            int index = orderList.SelectedIndex;
            if (index < 0) return;
            Snapshot();
            ordered.RemoveAt(index);
            RefreshOrder(index);
        };
        Action("↶ Deshacer").Click += (_, _) =>
        {
            if (history.Count == 0) return;
            ordered = history.Pop();
            RefreshOrder(0);
        };
        RefreshOrder(0);
        // Organizing remains in Apariencia; use stacked rows on narrow/high-DPI displays.
        appearance.Controls.Clear();
        var appearanceRows = PageRows(appearance);
        appearanceRows.Controls.Add(FieldRow(new Label { Text = "Estilo de los iconos:", AutoSize = true }, iconStyle));
        // Icon size belongs with all other appearance settings.
        var size = new NumericUpDown { Minimum = 16, Maximum = 64, Increment = 8, Value = Math.Clamp(config.IconSize, 16, 64), Width = 95 };
        var smallIcons = new CheckBox { Text = "Iconos pequeños (24 px)", AutoSize = true, Checked = config.IconSize <= 24 };
        smallIcons.CheckedChanged += (_, _) => { if (smallIcons.Checked) size.Value = 24; else if (size.Value == 24) size.Value = 48; };
        appearanceRows.Controls.Add(FieldRow(new Label { Text = "Tamaño de iconos (px):", AutoSize = true }, size, smallIcons));
        var barWidth = new NumericUpDown { Minimum = 64, Maximum = 300, Increment = 4, Width = 100, Value = Math.Clamp(config.Width, 64, 300) };
        var barHeight = new NumericUpDown { Minimum = 48, Maximum = 160, Increment = 4, Width = 100, Value = Math.Clamp(config.HorizontalHeight, 48, 160) };
        var animation = new CheckBox { Text = "Activar efecto lupa al pasar el mouse", AutoSize = true, Checked = config.HoverAnimation };
        var zoomChoice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 145 };
        zoomChoice.Items.AddRange(new object[] { "150 %", "180 %", "205 %", "240 %", "280 %", "340 %" });
        int[] zoomValues = { 150, 180, 205, 240, 280, 340 };
        int chosenZoom = Math.Clamp(config.ZoomPercent, 150, 340);
        zoomChoice.SelectedIndex = Array.IndexOf(zoomValues, chosenZoom);
        if (zoomChoice.SelectedIndex < 0) zoomChoice.SelectedIndex = 5;
        zoomChoice.Enabled = animation.Checked;
        animation.CheckedChanged += (_, _) => zoomChoice.Enabled = animation.Checked;
        appearanceRows.Controls.Add(new Label { Text = "Dimensiones de la barra", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) });
        appearanceRows.Controls.Add(FieldRow(new Label { Text = "Ancho barra vertical (px):", AutoSize = true }, barWidth));
        appearanceRows.Controls.Add(FieldRow(new Label { Text = "Alto barra horizontal (px):", AutoSize = true }, barHeight));
        appearanceRows.Controls.Add(animation);
        appearanceRows.Controls.Add(FieldRow(new Label { Text = "Ampliación de la lupa:", AutoSize = true }, zoomChoice));

        appearanceRows.Controls.Add(new Label { Text = "Windows conserva los iconos originales; Lucide usa trazos para accesos integrados.", AutoSize = true });
        appearanceRows.Controls.Add(new Label { Text = "Organizar accesos y separadores", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) });
        appearanceRows.Controls.Add(new Label { Text = "Arrastra filas o usa los botones. Guardar aplica los cambios.", AutoSize = true });
        orderList.Height = 170;
        appearanceRows.Controls.Add(orderList);
        actions.FlowDirection = FlowDirection.LeftToRight;
        actions.WrapContents = true;
        actions.AutoScroll = false;
        actions.AutoSize = false;
        actions.AutoScroll = false;
        actions.WrapContents = true;
        foreach (Control control in actions.Controls) { control.Width = Math.Max(116, control.PreferredSize.Width + 16); control.Height = Math.Max(34, control.PreferredSize.Height + 7); }
        appearanceRows.Controls.Add(actions);
        appearanceRows.SizeChanged += (_, _) => FitRows(appearanceRows);


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
        var powerTitle = new Label { Text = "Botones de energía en la barra:", AutoSize = true, Location = new Point(18, 292) };
        var shutdown = new CheckBox { Text = "Apagar", AutoSize = true, Location = new Point(240, 292), Checked = config.ShowShutdown };
        var restart = new CheckBox { Text = "Reiniciar", AutoSize = true, Location = new Point(335, 292), Checked = config.ShowRestart };
        general.Controls.AddRange(new Control[] { powerTitle, shutdown, restart });
        // Responsive layout: labels and inputs occupy separate rows so Windows 10
        // at 200% DPI cannot make them overlap. The page scrolls if necessary.
        general.Controls.Clear();
        var generalRows = PageRows(general);
        generalRows.Controls.Add(top);
        generalRows.Controls.Add(hide);
        generalRows.Controls.Add(startup);
        generalRows.Controls.Add(FieldRow(names, nameMode));
        generalRows.Controls.Add(FieldRow(new Label { Text = "Modo:", AutoSize = true }, dockMode));
        generalRows.Controls.Add(FieldRow(new Label { Text = "Ubicación:", AutoSize = true }, location));
        generalRows.Controls.Add(FieldRow(powerTitle, shutdown, restart));
        generalRows.Controls.Add(new Label { Text = "Al ocultarse queda una franja para recuperar la barra con el mouse.", AutoSize = true });
        generalRows.SizeChanged += (_, _) => FitRows(generalRows);

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

        // Disk settings use the same scrollable stack for 200% DPI.
        disks.Controls.Clear();
        var diskRows = PageRows(disks);
        diskRows.Controls.Add(widget);
        diskRows.Controls.Add(widgetLabel);
        diskRows.Controls.Add(FieldRow(new Label { Text = "Avisar desde (%):", AutoSize = true }, critical));
        diskPanel.Height = 220;
        diskPanel.AutoScroll = true;
        diskRows.Controls.Add(diskPanel);
        diskRows.SizeChanged += (_, _) => FitRows(diskRows);

        var info = new Label { Text = "JANUS Dock (LNK) — v5.0 (candidata)\n\nBarra de accesos rápidos para Windows 11.\nDesarrollada en C# con Windows Forms y .NET.\n\nIdeada en Pueblo Seco, Ñuble, Chile.\n16 de octubre de 2026.\n\nAutoría: Omar Aguila\nLaboratorios Momocrackcorp", Location = new Point(104, 20), Size = new Size(420, 215) };
        var appIcon = new PictureBox { Location = new Point(20, 25), Size = new Size(64, 64), SizeMode = PictureBoxSizeMode.Zoom };
        try { using var ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath); appIcon.Image = ico?.ToBitmap() ?? SystemIcons.Application.ToBitmap(); }
        catch { appIcon.Image = SystemIcons.Application.ToBitmap(); }
        about.Controls.Add(appIcon);
        about.Controls.Add(info);
        var updates = new Button { Text = "Buscar actualizaciones en GitHub", Location = new Point(20, 235), Size = new Size(260, 35) };
        updates.Click += (_, _) => Process.Start(new ProcessStartInfo("https://github.com/momocrackcorp/janus-windows/actions/workflows/build-janus-dock.yml") { UseShellExecute = true });
        about.Controls.Add(updates);
        about.Controls.Clear();
        var aboutRows = PageRows(about);
        appIcon.Size = new Size(64, 64);
        info.AutoSize = true;
        info.MaximumSize = new Size(480, 0);
        aboutRows.Controls.Add(appIcon);
        aboutRows.Controls.Add(info);
        updates.AutoSize = true;
        aboutRows.Controls.Add(updates);
        aboutRows.SizeChanged += (_, _) => FitRows(aboutRows);


        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 64, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(8), AutoScroll = false };
        Controls.Add(bar); bar.BringToFront();
        var cancel = new Button { Text = "Cancelar", AutoSize = true, MinimumSize = new Size(110, 36), DialogResult = DialogResult.Cancel };
        var apply = new Button { Text = "Guardar cambios", AutoSize = true, MinimumSize = new Size(150, 36), DialogResult = DialogResult.OK };
        bar.Controls.AddRange(new Control[] { apply, cancel });
        AcceptButton = apply; CancelButton = cancel;
        Shown += (_, _) =>
        {
            var area = Screen.FromControl(this).WorkingArea;
            Size = new Size(Math.Min(Math.Max(Width, (int)(area.Width * 0.78)), area.Width - 16), Math.Min(Math.Max(Height, (int)(area.Height * 0.80)), area.Height - 16));
            Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2), area.Top + Math.Max(0, (area.Height - Height) / 2));
            bar.Height = Math.Max(64, apply.PreferredSize.Height + 20);
            FitRows(generalRows);
            FitRows(appearanceRows);
            FitRows(diskRows);
            FitRows(aboutRows);
        };
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
            BarWidth = (int)barWidth.Value;
            BarHeight = (int)barHeight.Value;
            IconStyle = iconStyle.SelectedIndex == 1 ? "Lucide" : "Windows";
            OrderedItems = new List<DockItem>(ordered);
            Edge = (string)location.SelectedItem!;
            DiskColorArgb = config.DiskColorArgb;
            DiskColors = diskColors;
            HoverAnimation = animation.Checked;
            ZoomPercent = zoomValues[Math.Max(0, zoomChoice.SelectedIndex)];
            ShowShutdown = shutdown.Checked;
            ShowRestart = restart.Checked;
            CriticalPercent = (int)critical.Value;
        };
    }
    private static FlowLayoutPanel PageRows(TabPage page)
    {
        page.AutoScroll = true;
        var rows = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoScroll = false, Padding = new Padding(10), Margin = Padding.Empty,
            Location = Point.Empty, Width = Math.Max(340, page.ClientSize.Width - 24),
            Height = 630, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        page.Controls.Add(rows);
        page.Resize += (_, _) => FitRows(rows);
        return rows;
    }

    private static FlowLayoutPanel FieldRow(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
            Margin = new Padding(0, 3, 0, 8), Padding = Padding.Empty,
            MinimumSize = new Size(250, 36)
        };
        foreach (var control in controls)
        {
            control.Margin = new Padding(3, 5, 12, 5);
            if (control is ComboBox combo) combo.Width = Math.Min(combo.Width, 235);
            row.Controls.Add(control);
        }
        return row;
    }

    private static void FitRows(FlowLayoutPanel rows)
    {
        var page = rows.Parent;
        if (page == null) return;
        int width = Math.Max(290, page.ClientSize.Width - 28);
        rows.Width = width;
        foreach (Control control in rows.Controls)
        {
            if (control is ListBox list) list.Width = Math.Max(240, width - 24);
            if (control is FlowLayoutPanel row)
            {
                row.Width = Math.Max(250, width - 24);
                if (row.Controls.Count > 0 && row.Controls[0] is Button)
                {
                    // Button toolbar: adjust its height to the number of wrapped lines.
                    int usable = Math.Max(160, row.ClientSize.Width - row.Padding.Horizontal - 8);
                    int lineWidth = 0, lineHeight = 0, height = row.Padding.Vertical + 8;
                    foreach (Control button in row.Controls)
                    {
                        int itemWidth = button.Width + button.Margin.Horizontal;
                        int itemHeight = button.Height + button.Margin.Vertical;
                        if (lineWidth > 0 && lineWidth + itemWidth > usable)
                        {
                            height += lineHeight;
                            lineWidth = 0;
                            lineHeight = 0;
                        }
                        lineWidth += itemWidth;
                        lineHeight = Math.Max(lineHeight, itemHeight);
                    }
                    row.Height = height + lineHeight;
                }
            }
            if (control is Label label) label.MaximumSize = new Size(Math.Max(230, width - 25), 0);
            if (control is CheckBox check) check.MaximumSize = new Size(Math.Max(230, width - 25), 0);
            if (control is FlowLayoutPanel flow && flow != rows)
                foreach (Control child in flow.Controls)
                    if (child is Label rowLabel) rowLabel.MaximumSize = new Size(Math.Max(225, width - 38), 0);
        }
        rows.PerformLayout();
        rows.Height = rows.Controls.Cast<Control>().Sum(x => x.Height + x.Margin.Vertical) + rows.Padding.Vertical + 36;
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
