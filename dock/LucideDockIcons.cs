using System.Drawing;
using System.Drawing.Drawing2D;

namespace JanusDock;

// Thin 2px/24px vector icons following Lucide's familiar outline geometry.
// Only built-in dock commands/folders use these icons; application icons remain native.
internal static class LucideDockIcons
{
    public static Image? Load(string path, bool light)
    {
        string type = path.ToLowerInvariant() switch
        {
            "shell:mycomputerfolder" => "monitor",
            "janus:documents" => "folder",
            "janus:downloads" => "download",
            "janus:shutdown" => "power",
            "janus:restart" => "restart",
            _ => ""
        };
        if (type.Length == 0)
        {
            if (Directory.Exists(path)) type = "folder";
            else return null;
        }
        var bitmap = new Bitmap(96, 96);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(0, 0);
        g.ScaleTransform(4, 4);
        var color = type == "power" ? Color.FromArgb(220, 52, 47) :
                    type == "restart" ? Color.FromArgb(28, 170, 75) :
                    light ? Color.FromArgb(48, 57, 70) : Color.FromArgb(240, 242, 246);
        using var pen = new Pen(color, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        void Line(float x, float y, float xx, float yy) => g.DrawLine(pen, x, y, xx, yy);
        switch (type)
        {
            case "monitor":
                g.DrawRoundedRectangleCompat(pen, 2, 3, 20, 15, 2);
                Line(8, 22, 16, 22); Line(12, 18, 12, 22);
                break;
            case "folder":
                using (var pathShape = new GraphicsPath())
                {
                    pathShape.AddLines(new[] { new PointF(3, 20), new PointF(3, 5), new PointF(10, 5), new PointF(12, 8), new PointF(21, 8), new PointF(21, 20), new PointF(3, 20) });
                    g.DrawPath(pen, pathShape);
                }
                break;
            case "download":
                g.DrawRoundedRectangleCompat(pen, 3, 4, 18, 16, 2);
                Line(12, 7, 12, 16); Line(8, 12, 12, 16); Line(16, 12, 12, 16);
                break;
            case "power":
                // GDI+ angles are clockwise from 3 o’clock; leave the opening at the TOP.
                g.DrawArc(pen, 3, 3, 18, 18, -45, 270); Line(12, 2, 12, 12);
                break;
            case "restart":
                // Nearly closed loop with a small opening near the top-right arrow.
                g.DrawArc(pen, 3, 3, 18, 18, -58, 310);
                Line(20, 4, 20, 10); Line(20, 10, 14, 10);
                break;
        }
        return bitmap;
    }
    private static void DrawRoundedRectangleCompat(this Graphics g, Pen p, float x, float y, float width, float height, float radius)
    {
        using var path = new GraphicsPath();
        path.AddArc(x, y, radius * 2, radius * 2, 180, 90);
        path.AddArc(x + width - radius * 2, y, radius * 2, radius * 2, 270, 90);
        path.AddArc(x + width - radius * 2, y + height - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(x, y + height - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        g.DrawPath(p, path);
    }
}
