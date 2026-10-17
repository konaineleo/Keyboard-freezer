using System.Drawing;
using System.Drawing.Drawing2D;
using KeyboardTouchpadLock.Models;

namespace KeyboardTouchpadLock.Services;

/// <summary>
/// Draws a small circular icon (colored per LockState, with a glyph) at
/// runtime for use as the system tray icon. Avoids shipping static image
/// assets and guarantees the tray icon is always in sync with app state.
/// </summary>
public static class IconFactory
{
    public static Icon CreateStateIcon(LockState state, int sizePx = 32)
    {
        var color = ColorTranslator.FromHtml(state.ToColorHex());
        var glyph = state.ToGlyph();

        using var bitmap = new Bitmap(sizePx, sizePx);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 1, 1, sizePx - 2, sizePx - 2);

            using var font = new Font("Segoe UI Emoji", sizePx * 0.42f, FontStyle.Regular, GraphicsUnit.Pixel);
            var textSize = g.MeasureString(glyph, font);
            var origin = new PointF(
                (sizePx - textSize.Width) / 2f,
                (sizePx - textSize.Height) / 2f);
            g.DrawString(glyph, font, Brushes.White, origin);
        }

        IntPtr hIcon = bitmap.GetHicon();
        // Icon.FromHandle wraps the native handle; caller (NotifyIcon) copies
        // what it needs, but we should not destroy hIcon while the Icon is
        // still in use, so we deliberately leak the small native handle here -
        // it is reclaimed by Windows when the process exits. For a tray icon
        // that changes a handful of times per session this is negligible.
        return Icon.FromHandle(hIcon);
    }
}
