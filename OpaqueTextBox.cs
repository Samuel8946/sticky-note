using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace StickyNote;

/// <summary>
/// A text box that stays opaque while living inside Explorer's desktop window.
///
/// The desktop's surface carries an alpha channel, and DWM composites it. Managed GDI+
/// painting writes a full alpha byte, but the native EDIT control underneath a WinForms
/// TextBox paints with plain GDI, which leaves alpha at zero. Those pixels then get
/// composited additively, so the wallpaper burns through the note and bright wallpaper
/// washes it out to white.
///
/// After every paint this copies the freshly drawn pixels out, forces alpha to 255, and
/// writes them straight back with SourceCopy so GDI+ restores the alpha byte.
/// </summary>
internal sealed class OpaqueTextBox : TextBox
{
    private const int WM_PAINT = 0x000F;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WM_PAINT)
            RepairAlpha();
    }

    private void RepairAlpha()
    {
        Size size = ClientSize;
        if (size.Width <= 0 || size.Height <= 0 || !IsHandleCreated)
            return;

        try
        {
            using var target = Graphics.FromHwnd(Handle);
            using var snapshot = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);

            using (var into = Graphics.FromImage(snapshot))
            {
                IntPtr source = target.GetHdc();
                IntPtr destination = into.GetHdc();
                try
                {
                    Native.BitBlt(destination, 0, 0, size.Width, size.Height, source, 0, 0, Native.SRCCOPY);
                }
                finally
                {
                    into.ReleaseHdc(destination);
                    target.ReleaseHdc(source);
                }
            }

            ForceOpaque(snapshot);

            target.CompositingMode = CompositingMode.SourceCopy;
            target.DrawImageUnscaled(snapshot, 0, 0);
        }
        catch
        {
            // A dropped repair just means one frame looks washed out; never take the note down.
        }
    }

    private static unsafe void ForceOpaque(Bitmap bitmap)
    {
        var area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

        try
        {
            for (int y = 0; y < data.Height; y++)
            {
                byte* row = (byte*)data.Scan0 + (y * data.Stride);
                for (int x = 3; x < data.Width * 4; x += 4)
                    row[x] = 255;
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
