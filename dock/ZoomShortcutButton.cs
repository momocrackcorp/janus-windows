using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace JanusDock;

// Individual magnification on the same paint surface. No popup or transparency key.
public sealed class ZoomShortcutButton : Button
{
    public Image? SourceIcon { get; set; }
    public int BaseIconSize { get; set; } = 32;
    public bool ZoomEnabled { get; set; } = true;
    public bool ShowCaption { get; set; }
    private float zoom = 1f;
    private float goal = 1f;
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 16 };
    public ZoomShortcutButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        animation.Tick += (_, _) =>
        {
            zoom += (goal - zoom) * .34f;
            if (Math.Abs(goal - zoom) < .015f) { zoom = goal; animation.Stop(); }
            Invalidate();
        };
    }
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        goal = ZoomEnabled ? 2.05f : 1f;
        animation.Start();
    }
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        goal = 1f;
        animation.Start();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.Clear(BackColor);
        if (ClientRectangle.Contains(PointToClient(Cursor.Position)) && ZoomEnabled)
        {
            using var hover = new SolidBrush(Color.FromArgb(25, ForeColor));
            e.Graphics.FillRectangle(hover, ClientRectangle);
        }
        // Give the animation a visibly smaller resting size, leaving room to grow
        // inside narrow vertical bars without clipping at high DPI.
        float baseFactor = ZoomEnabled ? .80f : 1f;
        int size = (int)Math.Round(BaseIconSize * baseFactor * zoom);
        size = Math.Min(size, Math.Min(ClientSize.Width - 2, ClientSize.Height - (ShowCaption ? 29 : 2)));
        size = Math.Max(1, size);
        int x = (ClientSize.Width - size) / 2;
        int y = (ClientSize.Height - (ShowCaption ? 24 : 0) - size) / 2;
        if (ZoomEnabled) y = Math.Max(1, y - (int)(Math.Max(0, zoom - 1f) * 3));
        if (SourceIcon is not null) e.Graphics.DrawImage(SourceIcon, new Rectangle(x, y, size, size));
        if (ShowCaption)
        {
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(1, ClientSize.Height - 26, ClientSize.Width - 2, 25),
                ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak);
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { animation.Stop(); animation.Dispose(); SourceIcon?.Dispose(); }
        base.Dispose(disposing);
    }
}
