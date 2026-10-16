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
}
public class DockSettings
{
    public List<DockItem> Items { get; set; } = new();
    public int Width { get; set; } = 88;
    public int IconSize { get; set; } = 40;
    public bool ShowNames { get; set; } = false;
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
    const int ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3, ABE_RIGHT = 2;
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
    const uint SHGFI_ICON = 0x100, SHGFI_LARGEICON = 0, SHGFI_USEFILEATTRIBUTES = 0x10, FILE_ATTRIBUTE_NORMAL = 0x80;

    readonly string config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JANUS", "Dock", "config.json");
    readonly FlowLayoutPanel panel = new()
    {
        Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
        WrapContents = false, AutoScroll = true, Padding = new Padding(5)
    };
    readonly Panel grip = new() { Dock = DockStyle.Left, Width = 7, Cursor = Cursors.SizeWE };
    readonly Panel footer = new() { Dock = DockStyle.Bottom, Height = 44 };
    readonly Button add = new() { Text = "+", Width = 32, Height = 30, FlatStyle = FlatStyle.Flat };
    readonly Button options = new() { Text = "⚙", Width = 32, Height = 30, FlatStyle = FlatStyle.Flat };
    DockSettings settings = new();
    bool registered, repositioning, resizeActive, light;
    int dragX, dragWidth;
    readonly System.Windows.Forms.Timer themeTimer = new() { Interval = 3000 };

    AppBar Data() => new()
    {
        cbSize = Marshal.SizeOf<AppBar>(), hWnd = Handle,
        uCallbackMessage = CallbackMessage, uEdge = ABE_RIGHT
    };
    public Dock()
    {
        try { settings = JsonSerializer.Deserialize<DockSettings>(File.ReadAllText(config)) ?? new(); }
        catch { }
        settings.Width = Math.Clamp(settings.Width, 64, 300);
        settings.IconSize = Math.Clamp(settings.IconSize, 24, 64);
        Text = "JANUS Dock";
        Width = settings.Width;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
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
        grip.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                resizeActive = true;
                dragX = Cursor.Position.X;
                dragWidth = Width;
                grip.Capture = true;
            }
        };
        grip.MouseMove += (_, _) =>
        {
            if (resizeActive)
            {
                settings.Width = Math.Clamp(dragWidth + dragX - Cursor.Position.X, 64, 300);
                Width = settings.Width;
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
            return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 1)) != 0;
        }
        catch { return true; }
    }
    void ApplyTheme()
    {
        BackColor = light ? Color.FromArgb(245, 246, 249) : Color.FromArgb(32, 34, 40);
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
        int w = Math.Max(64, Width - grip.Width);
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
        if (!File.Exists(path) && !Directory.Exists(path) &&
            !(Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http"))) return;
        string name = Path.GetFileNameWithoutExtension(path.TrimEnd('\\', '/'));
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
        try
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http"))
                return (Image)SystemIcons.Information.ToBitmap();
            IntPtr result = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, out var info,
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
        int width = Math.Max(55, settings.Width - 18);
        foreach (var item in settings.Items.ToList())
        {
            var btn = new Button
            {
                Width = width,
                Height = settings.IconSize + (settings.ShowNames ? 45 : 14),
                Image = GetIcon(item.Path),
                ImageAlign = settings.ShowNames ? ContentAlignment.TopCenter : ContentAlignment.MiddleCenter,
                Text = settings.ShowNames ? item.Name : "",
                TextAlign = ContentAlignment.BottomCenter,
                TextImageRelation = TextImageRelation.ImageAboveText,
                AutoEllipsis = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = light ? Color.FromArgb(234, 237, 244) : Color.FromArgb(53, 58, 68),
                ForeColor = ForeColor,
                Font = new Font("Segoe UI", 8),
                Margin = new Padding(0, 3, 0, 3)
            };
            // WinForms Button.Image is drawn at native size; resize the extracted image to chosen dock size.
            var source = btn.Image;
            btn.Image = new Bitmap(source!, new Size(settings.IconSize, settings.IconSize));
            source?.Dispose();
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (_, _) => Open(item);
            var tips = new ToolTip { ShowAlways = true };
            tips.SetToolTip(btn, item.Name + "\n" + item.Path);
            var context = new ContextMenuStrip();
            context.Items.Add("Abrir", null, (_, _) => Open(item));
            context.Items.Add("Subir", null, (_, _) => Move(item, -1));
            context.Items.Add("Bajar", null, (_, _) => Move(item, 1));
            context.Items.Add("Quitar acceso", null, (_, _) => { settings.Items.Remove(item); Store(); Render(); });
            btn.ContextMenuStrip = context;
            btn.AllowDrop = true;
            btn.DragEnter += EnterDrag;
            btn.DragDrop += DropFiles;
            panel.Controls.Add(btn);
        }
        panel.ResumeLayout();
        AlignFooter();
    }
    void ShowOptions()
    {
        var menu = new ContextMenuStrip();
        var names = new ToolStripMenuItem("Mostrar nombres") { Checked = settings.ShowNames, CheckOnClick = true };
        names.CheckedChanged += (_, _) => { settings.ShowNames = names.Checked; Store(); Render(); };
        menu.Items.Add(names);
        var sizes = new ToolStripMenuItem("Tamaño de iconos");
        foreach (int size in new[] { 24, 32, 40, 48, 56, 64 })
        {
            var option = new ToolStripMenuItem(size + " px") { Checked = settings.IconSize == size };
            option.Click += (_, _) => { settings.IconSize = size; Store(); Render(); };
            sizes.DropDownItems.Add(option);
        }
        menu.Items.Add(sizes);
        menu.Items.Add("Cerrar JANUS Dock", null, (_, _) => Close());
        menu.Show(options, new Point(0, -menu.PreferredSize.Height));
    }
    void Move(DockItem item, int delta)
    {
        int i = settings.Items.IndexOf(item), n = i + delta;
        if (i < 0 || n < 0 || n >= settings.Items.Count) return;
        settings.Items.RemoveAt(i); settings.Items.Insert(n, item); Store(); Render();
    }
    void Open(DockItem item)
    {
        try { Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "No se pudo abrir", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    void Position()
    {
        if (repositioning || !registered) return;
        repositioning = true;
        try
        {
            var bounds = Screen.PrimaryScreen?.Bounds ?? Screen.FromControl(this).Bounds;
            var data = Data();
            data.rc = new Rect { left = bounds.Right - settings.Width, top = bounds.Top, right = bounds.Right, bottom = bounds.Bottom };
            SHAppBarMessage(ABM_QUERYPOS, ref data);
            data.rc.left = data.rc.right - settings.Width;
            SHAppBarMessage(ABM_SETPOS, ref data);
            Bounds = new Rectangle(data.rc.left, data.rc.top, settings.Width, data.rc.bottom - data.rc.top);
            foreach (Control c in panel.Controls) c.Width = Math.Max(55, settings.Width - 18);
        }
        finally { repositioning = false; }
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var data = Data();
        registered = SHAppBarMessage(ABM_NEW, ref data) != IntPtr.Zero;
        if (registered) Position();
        else
        {
            var b = Screen.PrimaryScreen!.Bounds;
            Bounds = new Rectangle(b.Right - settings.Width, b.Top, settings.Width, b.Height);
        }
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (registered && !resizeActive && (m.Msg == CallbackMessage || m.Msg == 0x007e)) Position();
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        themeTimer.Stop();
        if (registered)
        {
            var data = Data();
            SHAppBarMessage(ABM_REMOVE, ref data);
            registered = false;
        }
        base.OnFormClosing(e);
    }
}
