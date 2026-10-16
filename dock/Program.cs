using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace JanusDock;

public class DockItem
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsSeparator { get; set; } = false;
    public bool IsDiskWidget { get; set; } = false;
}
public class DockSettings
{
    public List<DockItem> Items { get; set; } = new();
    public int Width { get; set; } = 88;
    public int IconSize { get; set; } = 40;
    public bool ShowNames { get; set; } = false;
    public string NameMode { get; set; } = "Tooltip";
    public string DockMode { get; set; } = "Acoplada";
    public string Edge { get; set; } = "Derecha";
    public bool AlwaysOnTop { get; set; } = true;
    public bool AutoHide { get; set; } = false;
    public bool ShowDiskWidget { get; set; } = false;
    public int DiskColorArgb { get; set; } = Color.FromArgb(48, 130, 222).ToArgb();
    public int CriticalPercent { get; set; } = 90;
}
internal static class Program
{
    [STAThread] static void Main()
    {
        using var mutex = new Mutex(true, @"Local\JANUS_Dock_OnlyOne", out bool first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        Application.Run(new Dock());
    }
}
public class Dock : Form
{
    const int ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3;
    const int ABE_LEFT = 0, ABE_TOP = 1, ABE_RIGHT = 2, ABE_BOTTOM = 3;
    const int CallbackMessage = 0x8000 + 417;
    [StructLayout(LayoutKind.Sequential)]
    struct Rect { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)]
    struct AppBar
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uCallbackMessage, uEdge;
        public Rect rc;
        public IntPtr lParam;
    }
    [DllImport("shell32.dll")] static extern IntPtr SHAppBarMessage(int cmd, ref AppBar data);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 2;
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    class ShellLink { }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder file, int max, IntPtr data, uint flags);
        void GetIDList(out IntPtr ptr);
        void SetIDList(IntPtr ptr);
        void GetDescription(IntPtr text, int max);
        void SetDescription(string text);
        void GetWorkingDirectory(IntPtr text, int max);
        void SetWorkingDirectory(string text);
        void GetArguments(IntPtr text, int max);
        void SetArguments(string text);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int command);
        void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder icon, int max, out int index);
        void SetIconLocation(string path, int index);
        void SetRelativePath(string relative, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath(string path);
    }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010B-0000-0000-C000-000000000046")]
    interface IPersistFile { void GetClassID(out Guid id); void IsDirty(); void Load([MarshalAs(UnmanagedType.LPWStr)] string file, uint mode); void Save([MarshalAs(UnmanagedType.LPWStr)] string file, bool remember); void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string file); void GetCurFile(out IntPtr file); }
    const uint SHGFI_ICON = 0x100, SHGFI_LARGEICON = 0, SHGFI_USEFILEATTRIBUTES = 0x10, FILE_ATTRIBUTE_NORMAL = 0x80;

    readonly string config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JANUS", "Dock", "config.json");
    readonly FlowLayoutPanel panel = new()
    {
        Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
        WrapContents = false, AutoScroll = true, Padding = new Padding(5)
    };
    readonly Panel grip = new() { Dock = DockStyle.Left, Width = 7, Cursor = Cursors.SizeWE };
    readonly Panel footer = new() { Dock = DockStyle.Bottom, Height = 0, Visible = false };
    readonly Button add = new() { Text = "+", Width = 32, Height = 30, FlatStyle = FlatStyle.Flat };
    readonly Button options = new() { Text = "⚙", Width = 32, Height = 30, FlatStyle = FlatStyle.Flat };
    DockSettings settings = new();
    bool registered, repositioning, resizeActive, light;
    int dragX, dragWidth;
    readonly System.Windows.Forms.Timer themeTimer = new() { Interval = 3000 };
    readonly System.Windows.Forms.Timer hideTimer = new() { Interval = 400 };
    bool collapsed;
    const int RevealStrip = 6;

    AppBar Data() => new()
    {
        cbSize = Marshal.SizeOf<AppBar>(), hWnd = Handle,
        uCallbackMessage = CallbackMessage, uEdge = settings.Edge switch { "Izquierda" => ABE_LEFT, "Arriba" => ABE_TOP, "Abajo" => ABE_BOTTOM, _ => ABE_RIGHT }
    };
    public Dock()
    {
        try { settings = JsonSerializer.Deserialize<DockSettings>(File.ReadAllText(config)) ?? new(); }
        catch { }
        settings.Width = Math.Clamp(settings.Width, 64, 300);
        settings.IconSize = Math.Clamp(settings.IconSize, 16, 64);
        if (settings.ShowNames && settings.NameMode == "Tooltip") settings.NameMode = "Written";
        if (settings.DockMode != "Flotante") settings.DockMode = "Acoplada";
        settings.CriticalPercent = Math.Clamp(settings.CriticalPercent, 70, 99);
        // Migrate old disk widget into the reorderable item list once.
        if (settings.ShowDiskWidget && !settings.Items.Any(x => x.IsDiskWidget))
            settings.Items.Insert(0, new DockItem { Name = "Discos", IsDiskWidget = true });
        if (!new[] { "Derecha", "Izquierda", "Arriba", "Abajo" }.Contains(settings.Edge)) settings.Edge = "Derecha";
        Text = "JANUS Dock";
        Width = settings.Width;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = settings.AlwaysOnTop;
        AllowDrop = true;
        MinimumSize = new Size(64, 200);
        add.Click += (_, _) => AddDialog();
        options.Click += (_, _) => ShowOptions();
        add.FlatAppearance.BorderSize = 0;
        options.FlatAppearance.BorderSize = 0;
        footer.Controls.Add(add);
        footer.Controls.Add(options);
        Controls.Add(panel);
        Controls.Add(footer);
        Controls.Add(grip);
        SetOrientation();
        panel.ContextMenuStrip = MakeDockMenu();
        grip.ContextMenuStrip = MakeDockMenu();
        ContextMenuStrip = MakeDockMenu();
        grip.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                resizeActive = true;
                dragX = Horizontal ? Cursor.Position.Y : Cursor.Position.X;
                dragWidth = settings.Width;
                grip.Capture = true;
            }
        };
        grip.MouseMove += (_, _) =>
        {
            if (resizeActive)
            {
                int current = Horizontal ? Cursor.Position.Y : Cursor.Position.X;
                int sign = settings.Edge is "Izquierda" or "Arriba" ? 1 : -1;
                settings.Width = Math.Clamp(dragWidth + sign * (current - dragX), 64, 300);
                Position();
                AlignFooter();
            }
        };
        grip.MouseUp += (_, _) => FinishResize();
        grip.MouseCaptureChanged += (_, _) => { if (resizeActive) FinishResize(); };
        Resize += (_, _) => AlignFooter();
        DragEnter += EnterDrag;
        DragDrop += DropFiles;
        panel.AllowDrop = true;
        panel.DragEnter += EnterDrag;
        panel.DragDrop += DropFiles;
        light = IsLightTheme();
        ApplyTheme();
        Render();
        themeTimer.Tick += (_, _) =>
        {
            bool next = IsLightTheme();
            if (next != light) { light = next; ApplyTheme(); Render(); }
        };
        themeTimer.Start();
        hideTimer.Tick += (_, _) => UpdateAutoHide();
        hideTimer.Start();
    }
    void FinishResize()
    {
        if (!resizeActive) return;
        resizeActive = false;
        Store();
        Position();
        Render();
    }
    bool IsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return Convert.ToInt32(key?.GetValue("SystemUsesLightTheme", 1)) != 0;
        }
        catch { return true; }
    }
    Color TaskbarColor()
    {
        // DWM system accent is not guaranteed to equal Windows 11 taskbar material.
        // Blend it subtly with the system light/dark surface for a closer match.
        Color surface = light ? Color.FromArgb(245, 246, 249) : Color.FromArgb(32, 34, 40);
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            int raw = Convert.ToInt32(key?.GetValue("ColorizationColor", 0));
            if (raw == 0) return surface;
            var accent = Color.FromArgb((raw >> 16) & 255, (raw >> 8) & 255, raw & 255);
            double mix = light ? 0.10 : 0.18;
            return Color.FromArgb((int)(surface.R*(1-mix)+accent.R*mix), (int)(surface.G*(1-mix)+accent.G*mix), (int)(surface.B*(1-mix)+accent.B*mix));
        }
        catch { return surface; }
    }
    void ApplyTheme()
    {
        BackColor = TaskbarColor();
        ForeColor = light ? Color.FromArgb(28, 30, 35) : Color.FromArgb(241, 241, 244);
        footer.BackColor = BackColor;
        panel.BackColor = BackColor;
        grip.BackColor = light ? Color.FromArgb(210, 216, 226) : Color.FromArgb(66, 71, 82);
        foreach (var button in new[] { add, options })
        {
            button.BackColor = light ? Color.FromArgb(228, 233, 242) : Color.FromArgb(58, 62, 72);
            button.ForeColor = ForeColor;
        }
    }
    void AlignFooter()
    {
        int w = Horizontal ? 80 : Math.Max(64, Width - grip.Width);
        add.Location = new Point(Math.Max(5, (w - 72) / 2), 7);
        options.Location = new Point(add.Right + 6, 7);
    }
    void Store()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        string temp = config + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, config, true);
    }
    void Add(string path)
    {
        if (path != "shell:MyComputerFolder" && !File.Exists(path) && !Directory.Exists(path) &&
            !(Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http"))) return;
        string name = path == "shell:MyComputerFolder" ? "Este equipo" : Path.GetFileNameWithoutExtension(path.TrimEnd('\\', '/'));
        settings.Items.Add(new DockItem { Name = string.IsNullOrWhiteSpace(name) ? path : name, Path = path });
        Store(); Render();
    }
    void AddDialog()
    {
        using var f = new OpenFileDialog { Title = "Agregar programa o acceso directo", Filter = "Archivos y accesos|*.exe;*.lnk;*.url;*.*" };
        if (f.ShowDialog(this) == DialogResult.OK) Add(f.FileName);
        else
        {
            string target = Microsoft.VisualBasic.Interaction.InputBox("Ruta de carpeta o dirección web:", "JANUS Dock", "");
            if (!string.IsNullOrWhiteSpace(target)) Add(target.Trim());
        }
    }
    void EnterDrag(object? sender, DragEventArgs e) =>
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
    void DropFiles(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
            foreach (string path in paths) Add(path);
    }
    Image GetIcon(string path)
    {
        // Resolve the underlying target/icon of .lnk rather than asking the shell for
        // the shortcut composite (which contains the arrow overlay).
        if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var obj = new ShellLink();
                try
                {
                    ((IPersistFile)obj).Load(path, 0);
                    var link = (IShellLinkW)obj;
                    var iconPath = new System.Text.StringBuilder(1024);
                    link.GetIconLocation(iconPath, iconPath.Capacity, out int iconIndex);
                    if (iconPath.Length > 0 && File.Exists(Environment.ExpandEnvironmentVariables(iconPath.ToString())))
                    {
                        using var ico = Icon.ExtractAssociatedIcon(Environment.ExpandEnvironmentVariables(iconPath.ToString()));
                        if (ico != null) return ico.ToBitmap();
                    }
                    var target = new System.Text.StringBuilder(1024);
                    link.GetPath(target, target.Capacity, IntPtr.Zero, 0);
                    if (target.Length > 0 && File.Exists(target.ToString()))
                    {
                        using var ico = Icon.ExtractAssociatedIcon(target.ToString());
                        if (ico != null) return ico.ToBitmap();
                    }
                }
                finally { Marshal.FinalReleaseComObject(obj); }
            }
            catch { }
        }
        try
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http"))
                return (Image)SystemIcons.Information.ToBitmap();
            if (path == "shell:MyComputerFolder")
            {
                // Native Explorer/This PC icon from the system library.
                using var pcIcon = Icon.ExtractAssociatedIcon(Path.Combine(Environment.SystemDirectory, "explorer.exe"));
                if (pcIcon != null) return pcIcon.ToBitmap();
            }
            string iconPath = path;
            IntPtr result = SHGetFileInfo(iconPath, FILE_ATTRIBUTE_NORMAL, out var info,
                (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
            if (result != IntPtr.Zero && info.hIcon != IntPtr.Zero)
            {
                try { using var icon = (Icon)Icon.FromHandle(info.hIcon).Clone(); return icon.ToBitmap(); }
                finally { DestroyIcon(info.hIcon); }
            }
        }
        catch { }
        return (Image)SystemIcons.Application.ToBitmap();
    }
    void Render()
    {
        panel.SuspendLayout();
        while (panel.Controls.Count > 0)
        {
            var control = panel.Controls[0];
            panel.Controls.RemoveAt(0);
            if (control is Button b) b.Image?.Dispose();
            control.Dispose();
        }
        int width = Horizontal ? Math.Max(60, settings.IconSize + (settings.NameMode == "Written" ? 55 : 12)) : Math.Max(55, settings.Width - 18);
        foreach (var item in settings.Items.ToList())
        {
            if (item.IsDiskWidget)
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!drive.IsReady || drive.TotalSize <= 0) continue;
                        var view = new DiskUsageControl(drive, Color.FromArgb(settings.DiskColorArgb), settings.CriticalPercent, true)
                        {
                            Width = Horizontal ? 72 : Math.Max(65, settings.Width - 16), Height = 67, BackColor = BackColor, ForeColor = ForeColor,
                            Margin = new Padding(0, 1, 0, 1)
                        };
                        var diskMenu = new ContextMenuStrip();
                        diskMenu.Items.Add("Mover bloque de discos hacia arriba", null, (_, _) => Move(item, -1));
                        diskMenu.Items.Add("Mover bloque de discos hacia abajo", null, (_, _) => Move(item, 1));
                        diskMenu.Items.Add("Opciones...", null, (_, _) => ShowOptions());
                        view.ContextMenuStrip = diskMenu;
                        view.Margin = Horizontal ? new Padding(2, 1, 2, 1) : new Padding(Math.Max(0, (settings.Width - 16 - view.Width) / 2), 1, 0, 1);
                        panel.Controls.Add(view);
                    }
                    catch { }
                }
                continue;
            }
            if (item.IsSeparator)
            {
                var sep = new Panel { Width = Horizontal ? 2 : Math.Max(30, settings.Width - 25), Height = Horizontal ? Math.Max(30, settings.Width - 25) : 2, Margin = new Padding(8, 6, 8, 6), BackColor = light ? Color.FromArgb(170, 178, 190) : Color.FromArgb(105, 112, 125) };
                var separatorMenu = new ContextMenuStrip();
                separatorMenu.Items.Add("Quitar separador", null, (_, _) => { settings.Items.Remove(item); Store(); Render(); });
                separatorMenu.Items.Add("Subir", null, (_, _) => Move(item, -1));
                separatorMenu.Items.Add("Bajar", null, (_, _) => Move(item, 1));
                sep.ContextMenuStrip = separatorMenu;
                panel.Controls.Add(sep);
                continue;
            }
            var btn = new Button
            {
                Width = width,
                Height = Horizontal ? Math.Clamp(settings.IconSize + (settings.NameMode == "Written" ? 28 : 10), 40, 92) : settings.IconSize + (settings.NameMode == "Written" ? 45 : 14),
                Image = GetIcon(item.Path),
                ImageAlign = settings.NameMode == "Written" ? ContentAlignment.TopCenter : ContentAlignment.MiddleCenter,
                Text = settings.NameMode == "Written" ? item.Name : "",
                TextAlign = ContentAlignment.BottomCenter,
                TextImageRelation = TextImageRelation.ImageAboveText,
                AutoEllipsis = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = BackColor,
                ForeColor = ForeColor,
                Font = new Font("Segoe UI", 8),
                Margin = new Padding(0, 2, 0, 2)
            };
            // WinForms Button.Image is drawn at native size; resize the extracted image to chosen dock size.
            var source = btn.Image;
            btn.Image = new Bitmap(source!, new Size(settings.IconSize, settings.IconSize));
            source?.Dispose();
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = light ? Color.FromArgb(225, 233, 244) : Color.FromArgb(62, 67, 77);
            btn.FlatAppearance.MouseDownBackColor = light ? Color.FromArgb(210, 222, 239) : Color.FromArgb(78, 83, 97);
            btn.Click += (_, _) => Open(item);
            var tips = new ToolTip { ShowAlways = true };
            if (settings.NameMode != "None") tips.SetToolTip(btn, item.Name + "\n" + item.Path);
            var context = new ContextMenuStrip();
            context.Items.Add("Abrir", null, (_, _) => Open(item));
            context.Items.Add("Subir", null, (_, _) => Move(item, -1));
            context.Items.Add("Bajar", null, (_, _) => Move(item, 1));
            context.Items.Add("Quitar acceso", null, (_, _) => { settings.Items.Remove(item); Store(); Render(); });
            context.Items.Add(new ToolStripSeparator());
            context.Items.Add("Opciones de la barra", null, (_, _) => ShowOptions());
            btn.ContextMenuStrip = context;
            btn.AllowDrop = true;
            btn.DragEnter += EnterDrag;
            btn.DragDrop += DropFiles;
            panel.Controls.Add(btn);
        }
        panel.ResumeLayout();
        AlignFooter();
    }
    ToolStripMenuItem DrivesMenu()
    {
        var menu = new ToolStripMenuItem("Agregar unidad de disco");
        foreach (var drive in DriveInfo.GetDrives())
        {
            string path = drive.Name;
            string label;
            try { label = drive.IsReady ? $"{drive.VolumeLabel} ({path.TrimEnd('\\')})" : $"Unidad {path.TrimEnd('\\')} (no disponible)"; }
            catch { label = path; }
            menu.DropDownItems.Add(label, null, (_, _) => Add(path));
        }
        return menu;
    }
    ContextMenuStrip MakeDockMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            menu.Items.Add("Agregar acceso...", null, (_, _) => AddDialog());
            menu.Items.Add("Agregar Este equipo", null, (_, _) => Add("shell:MyComputerFolder"));
            menu.Items.Add(DrivesMenu());
            menu.Items.Add("Agregar separador", null, (_, _) => { settings.Items.Add(new DockItem { Name = "Separador", IsSeparator = true }); Store(); Render(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Opciones...", null, (_, _) => ShowOptions());
            menu.Items.Add("Refrescar barra", null, (_, _) => RefreshDock());
            menu.Items.Add("Salir de JANUS Dock", null, (_, _) => Close());
        };
        return menu;
    }
    void ShowOptions()
    {
        using var dlg = new DockOptionsForm(settings);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        UnregisterBar();
        settings.AlwaysOnTop = dlg.AlwaysOnTop;
        settings.AutoHide = dlg.AutoHide;
        settings.ShowNames = dlg.ShowNames;
        settings.NameMode = dlg.NameMode;
        settings.DockMode = dlg.DockMode;
        settings.ShowDiskWidget = dlg.ShowDiskWidget;
        settings.DiskColorArgb = dlg.DiskColorArgb;
        settings.CriticalPercent = dlg.CriticalPercent;
        var currentWidget = settings.Items.FirstOrDefault(i => i.IsDiskWidget);
        if (settings.ShowDiskWidget && currentWidget == null) settings.Items.Add(new DockItem { Name = "Discos", IsDiskWidget = true });
        if (!settings.ShowDiskWidget && currentWidget != null) settings.Items.Remove(currentWidget);
        settings.IconSize = dlg.IconSize;
        settings.Edge = dlg.Edge;
        TopMost = settings.AlwaysOnTop;
        collapsed = false;
        DockOptionsForm.SetStartup(dlg.StartWithWindows);
        SetOrientation();
        RegisterBar();
        Store();
        Render();
    }
    void ShowLegacyOptions()
    {
        var menu = new ContextMenuStrip();
        var names = new ToolStripMenuItem("Mostrar nombres") { Checked = settings.ShowNames, CheckOnClick = true };
        names.CheckedChanged += (_, _) => { settings.ShowNames = names.Checked; Store(); Render(); };
        menu.Items.Add(names);
        menu.Items.Add("Agregar acceso...", null, (_, _) => AddDialog());
        menu.Items.Add("Agregar separador", null, (_, _) => { settings.Items.Add(new DockItem { Name = "Separador", IsSeparator = true }); Store(); Render(); });
        var locations = new ToolStripMenuItem("Ubicación");
        foreach (string edge in new[] { "Derecha", "Izquierda", "Arriba", "Abajo" })
        {
            var opt = new ToolStripMenuItem(edge) { Checked = settings.Edge == edge };
            opt.Click += (_, _) => { if (settings.Edge != edge) { UnregisterBar(); settings.Edge = edge; SetOrientation(); RegisterBar(); Store(); Render(); } };
            locations.DropDownItems.Add(opt);
        }
        menu.Items.Add(locations);
        var sizes = new ToolStripMenuItem("Tamaño de iconos");
        foreach (int size in new[] { 24, 32, 40, 48, 56, 64 })
        {
            var option = new ToolStripMenuItem(size + " px") { Checked = settings.IconSize == size };
            option.Click += (_, _) => { settings.IconSize = size; Store(); Render(); };
            sizes.DropDownItems.Add(option);
        }
        menu.Items.Add(sizes);
        menu.Items.Add("Refrescar barra", null, (_, _) => RefreshDock());
        menu.Items.Add("Cerrar JANUS Dock", null, (_, _) => Close());
        menu.Show(this, PointToClient(Cursor.Position));
    }
    void RefreshDock()
    {
        light = IsLightTheme(); ApplyTheme();
        collapsed = false;
        UnregisterBar();
        RegisterBar();
        Render();
        Position();
    }
    void Move(DockItem item, int delta)
    {
        int i = settings.Items.IndexOf(item), n = i + delta;
        if (i < 0 || n < 0 || n >= settings.Items.Count) return;
        settings.Items.RemoveAt(i); settings.Items.Insert(n, item); Store(); Render();
    }
    void Open(DockItem item)
    {
        try
        {
            if (item.Path == "shell:MyComputerFolder") Process.Start(new ProcessStartInfo("explorer.exe", "shell:MyComputerFolder") { UseShellExecute = true });
            else Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "No se pudo abrir", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    bool Horizontal => settings.Edge == "Arriba" || settings.Edge == "Abajo";
    void SetOrientation()
    {
        panel.FlowDirection = Horizontal ? FlowDirection.LeftToRight : FlowDirection.TopDown;
        panel.Dock = DockStyle.Fill;
        footer.Dock = Horizontal ? DockStyle.Right : DockStyle.Bottom;
        footer.Width = Horizontal ? 80 : settings.Width;
        footer.Height = 0;
        footer.Visible = false;
        MinimumSize = Horizontal ? new Size(200, 1) : new Size(64, 200);
        grip.Dock = settings.Edge switch
        {
            "Izquierda" => DockStyle.Right,
            "Arriba" => DockStyle.Bottom,
            "Abajo" => DockStyle.Top,
            _ => DockStyle.Left
        };
        grip.Cursor = Horizontal ? Cursors.SizeNS : Cursors.SizeWE;
        grip.Visible = settings.Edge != "Abajo" && settings.DockMode != "Flotante";
        FormBorderStyle = FormBorderStyle.None;
        if (settings.DockMode == "Flotante")
        {
            MinimumSize = new Size(65, 65);
            panel.MouseDown -= DragFloating;
            panel.MouseDown += DragFloating;
        }
        else panel.MouseDown -= DragFloating;
        AlignFooter();
    }
    void DragFloating(object? sender, MouseEventArgs e)
    {
        if (settings.DockMode != "Flotante" || e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
    }
    void UnregisterBar()
    {
        if (!registered) return;
        var d = Data();
        SHAppBarMessage(ABM_REMOVE, ref d);
        registered = false;
    }
    void RegisterBar()
    {
        if (!IsHandleCreated) return;
        // Windows 11 owns the lower edge with its taskbar. Do not register
        // another AppBar there; use the safe work area instead.
        if (settings.DockMode == "Flotante" || settings.Edge == "Abajo") { registered = false; Position(); return; }
        var d = Data();
        registered = SHAppBarMessage(ABM_NEW, ref d) != IntPtr.Zero;
        Position();
    }
    void UpdateAutoHide()
    {
        if (settings.DockMode == "Flotante" || !settings.AutoHide || !Visible || resizeActive)
        {
            if (collapsed) { collapsed = false; Position(); }
            return;
        }
        var mouse = Cursor.Position;
        bool shouldExpand = Bounds.Contains(mouse);
        if (collapsed)
        {
            var screen = Screen.PrimaryScreen?.Bounds ?? Screen.FromControl(this).Bounds;
            shouldExpand = settings.Edge switch
            {
                "Derecha" => mouse.X >= screen.Right - 12,
                "Izquierda" => mouse.X <= screen.Left + 12,
                "Arriba" => mouse.Y <= screen.Top + 12,
                _ => mouse.Y >= (Screen.PrimaryScreen?.WorkingArea.Bottom ?? screen.Bottom) - 12 && mouse.Y < (Screen.PrimaryScreen?.WorkingArea.Bottom ?? screen.Bottom)
            };
        }
        if (shouldExpand == collapsed) { collapsed = !collapsed; Position(); }
    }
    void Position()
    {
        if (repositioning) return;
        repositioning = true;
        try
        {
            var bounds = Screen.PrimaryScreen?.Bounds ?? Screen.FromControl(this).Bounds;
            if (settings.DockMode == "Flotante")
            {
                panel.Visible = !collapsed;
                if (Width < 65 || Height < 65 || (Left == 0 && Top == 0)) Bounds = new Rectangle(bounds.Left + 140, bounds.Top + 120, 110, 450);
                return;
            }
            int thickness = collapsed ? RevealStrip : (Horizontal ? Math.Clamp(settings.IconSize + (settings.ShowNames ? 36 : 16), 48, 100) : settings.Width);
            if (settings.Edge == "Abajo")
            {
                // Do not overlap the Windows taskbar or reserve a giant bottom strip.
                var work = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromControl(this).WorkingArea;
                Bounds = new Rectangle(work.Left, work.Bottom - thickness, work.Width, thickness);
                panel.Visible = !collapsed;
                foreach (Control c in panel.Controls)
                {
                    if (c is Button b) { b.Width = Math.Max(52, settings.IconSize + (settings.ShowNames ? 55 : 12)); b.Height = thickness - 8; }
                }
                return;
            }
            var d = Data();
            d.rc = new Rect
            {
                left = settings.Edge == "Derecha" ? bounds.Right - thickness : bounds.Left,
                right = settings.Edge == "Izquierda" ? bounds.Left + thickness : bounds.Right,
                top = settings.Edge == "Abajo" ? bounds.Bottom - thickness : bounds.Top,
                bottom = settings.Edge == "Arriba" ? bounds.Top + thickness : bounds.Bottom
            };
            if (registered)
            {
                SHAppBarMessage(ABM_QUERYPOS, ref d);
                switch (settings.Edge)
                {
                    case "Izquierda": d.rc.right = d.rc.left + thickness; break;
                    case "Arriba": d.rc.bottom = d.rc.top + thickness; break;
                    case "Abajo": d.rc.top = d.rc.bottom - thickness; break;
                    default: d.rc.left = d.rc.right - thickness; break;
                }
                SHAppBarMessage(ABM_SETPOS, ref d);
            }
            Bounds = Rectangle.FromLTRB(d.rc.left, d.rc.top, d.rc.right, d.rc.bottom);
            panel.Visible = !collapsed;
            footer.Width = Horizontal ? 80 : settings.Width;
            foreach (Control c in panel.Controls)
            {
                if (c is Button b)
                {
                    b.Width = Horizontal ? Math.Max(60, settings.IconSize + (settings.ShowNames ? 55 : 12)) : Math.Max(55, thickness - 18);
                    b.Height = Horizontal ? Math.Max(52, thickness - 17) : settings.IconSize + (settings.ShowNames ? 45 : 14);
                }
            }
        }
        finally { repositioning = false; }
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SetOrientation();
        RegisterBar();
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (registered && !resizeActive && (m.Msg == CallbackMessage || m.Msg == 0x007e)) Position();
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        themeTimer.Stop();
        hideTimer.Stop();
        UnregisterBar();
        base.OnFormClosing(e);
    }
}
