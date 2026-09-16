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
/// After every WM_PAINT this copies the freshly drawn pixels out, forces alpha to 255, and
/// writes them straight back with SourceCopy so GDI+ restores the alpha byte. That repair
/// is synchronous and unthrottled: skipping even one paint lets DWM present a transparent
/// frame.
///
/// The text caret is a second, independent problem. Windows blinks it by XOR-drawing
/// straight to the window's DC, which never goes through WM_PAINT. Capturing while that
/// inverted caret is on-screen would bake the inversion in as permanent opaque content.
/// HideCaret around the copy so the capture always sees the true glyphs, not a blink frame.
/// </summary>
internal sealed class OpaqueTextBox : TextBox
{
    private const int WM_PAINT = 0x000F;

    private Bitmap? _buffer;
    private Graphics? _bufferGraphics;
    private Size _bufferSize;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WM_PAINT)
            RepairAlpha();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bufferGraphics?.Dispose();
            _buffer?.Dispose();
            _bufferGraphics = null;
            _buffer = null;
        }

        base.Dispose(disposing);
    }

    private void RepairAlpha()
    {
        try
        {
            if (!IsHandleCreated || !Visible)
                return;

            Size size = ClientSize;
            if (size.Width <= 0 || size.Height <= 0)
                return;

            if (!EnsureBuffer(size))
                return;

            using Graphics target = Graphics.FromHwnd(Handle);

            // Fill first so a clipped BitBlt cannot re-stamp leftover glyphs from a
            // previous frame into the occluded strip.
            _bufferGraphics!.Clear(Color.FromArgb(255, BackColor));

            bool caretHidden = Native.HideCaret(Handle);
            try
            {
                IntPtr source = IntPtr.Zero;
                IntPtr destination = IntPtr.Zero;
                bool copied;
                try
                {
                    source = target.GetHdc();
                    destination = _bufferGraphics.GetHdc();
                    copied = Native.BitBlt(destination, 0, 0, size.Width, size.Height, source, 0, 0, Native.SRCCOPY);
                }
                finally
                {
                    if (destination != IntPtr.Zero)
                        _bufferGraphics.ReleaseHdc(destination);
                    if (source != IntPtr.Zero)
                        target.ReleaseHdc(source);
                }

                if (!copied)
                    return;

                ForceOpaque(_buffer!);

                target.CompositingMode = CompositingMode.SourceCopy;
                target.DrawImageUnscaled(_buffer!, 0, 0);
            }
            finally
            {
                if (caretHidden)
                    Native.ShowCaret(Handle);
            }
        }
        catch
        {
            // One dropped paint repair looks washed out for a frame. Crashing the note does not.
        }
    }

    /// <summary>
    /// Builds the replacement buffer completely before swapping it in, so a throw mid-resize
    /// cannot leave the fields pointing at disposed objects.
    /// </summary>
    private bool EnsureBuffer(Size size)
    {
        if (_buffer is not null && _bufferGraphics is not null && _bufferSize == size)
            return true;

        Bitmap bitmap;
        Graphics graphics;
        try
        {
            bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        }
        catch
        {
            return false;
        }

        try
        {
            graphics = Graphics.FromImage(bitmap);
        }
        catch
        {
            bitmap.Dispose();
            return false;
        }

        _bufferGraphics?.Dispose();
        _buffer?.Dispose();
        _buffer = bitmap;
        _bufferGraphics = graphics;
        _bufferSize = size;
        return true;
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
