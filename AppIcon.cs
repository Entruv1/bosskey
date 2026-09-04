using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace BossKey;

internal static class AppIcon
{
    internal static Icon Create()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.Clear(Color.Transparent);

            using var path = RoundedRect(new Rectangle(1, 1, 30, 30), 8);
            using var brush = new LinearGradientBrush(
                new Point(2, 2), new Point(30, 30),
                Color.FromArgb(44, 110, 205), Color.FromArgb(22, 58, 120));
            g.FillPath(brush, path);
            g.DrawPath(Pens.White, path);

            using var font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold, GraphicsUnit.Pixel);
            g.DrawString("隐", font, Brushes.White, 6F, 5F);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using (var temp = Icon.FromHandle(hIcon))
                return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
