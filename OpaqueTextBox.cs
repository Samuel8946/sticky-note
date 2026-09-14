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
///
/// The text caret is a second, independent problem: Windows blinks it by XOR-drawing
/// straight to the window's DC on a hidden system timer, which never goes through WM_PAINT
/// at all. If this class's repair happens to run while the caret is mid-blink (most likely
/// under the rapid-fire WM_PAINT bursts a window drag produces), it captures that inverted
/// pixel and re-stamps it as permanent opaque content -- visible as a lingering "highlight"
/// that only clears once something forces a real content repaint of that spot (e.g. the
/// text reflowing after a backspace).
///
/// Two mitigations keep that from sticking: HideCaret around the blit so a mid-blink frame
/// is never captured, and SuspendRepairs while the parent is dragging so the paint storm
/// from SetWindowPos does not race the caret timer. A periodic heal still clears any stray
/// artifact within a fraction of a second if one slips through.
/// </summary>
internal sealed class OpaqueTextBox : TextBox
{
    private const int WM_PAINT = 0x000F;

    // Below this, back-to-back repairs (a WM_PAINT immediately followed by a timer tick)
    // just redo the same work; above it, a stray caret-blink artifact is visible too long.
    private const int MinRepairIntervalMs = 60;

    private readonly System.Windows.Forms.Timer _healTimer;
    private int _lastRepairTick;
    private bool _repairsSuspended;

    private Bitmap? _buffer;
    private Graphics? _bufferGraphics;
    private Size _bufferSize;

    public OpaqueTextBox()
    {
        _healTimer = new System.Windows.Forms.Timer { Interval = 120 };
        _healTimer.Tick += (_, _) => RepairAlpha();
        _healTimer.Start();
    }

    /// <summary>
    /// Pause alpha repairs during move/resize. Drag produces a WM_PAINT storm via ForceRedraw;
    /// repairing every frame races the caret blink and is the usual source of flicker.
    /// </summary>
    public void SuspendRepairs() => _repairsSuspended = true;

    /// <summary>Resume repairs and run one immediately so the surface is opaque again.</summary>
    public void ResumeRepairs()
    {
        _repairsSuspended = false;
        _lastRepairTick = 0;
        RepairAlpha();
    }

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
            _healTimer.Dispose();
            _bufferGraphics?.Dispose();
            _buffer?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RepairAlpha()
    {
        if (_repairsSuspended)
            return;

        int now = Environment.TickCount;
        if (unchecked(now - _lastRepairTick) < MinRepairIntervalMs)
            return;

        Size size = ClientSize;
        if (size.Width <= 0 || size.Height <= 0 || !IsHandleCreated)
            return;

        try
        {
            EnsureBuffer(size);

            using Graphics target = Graphics.FromHwnd(Handle);

            // HideCaret is cumulative and no-ops when this window does not own the caret.
            Native.HideCaret(Handle);
            try
            {
                IntPtr source = target.GetHdc();
                IntPtr destination = _bufferGraphics!.GetHdc();
                try
                {
                    Native.BitBlt(destination, 0, 0, size.Width, size.Height, source, 0, 0, Native.SRCCOPY);
                }
                finally
                {
                    _bufferGraphics.ReleaseHdc(destination);
                    target.ReleaseHdc(source);
                }

                ForceOpaque(_buffer!);

                target.CompositingMode = CompositingMode.SourceCopy;
                target.DrawImageUnscaled(_buffer!, 0, 0);
            }
            finally
            {
                Native.ShowCaret(Handle);
            }

            _lastRepairTick = now;
        }
        catch
        {
            // A dropped repair just means one frame looks washed out; never take the note down.
        }
    }

    /// <summary>Reused across repairs instead of allocated per-call, since the timer means this
    /// now runs continuously rather than only on genuine content changes.</summary>
    private void EnsureBuffer(Size size)
    {
        if (_buffer is not null && _bufferSize == size)
            return;

        _bufferGraphics?.Dispose();
        _buffer?.Dispose();

        _buffer = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        _bufferGraphics = Graphics.FromImage(_buffer);
        _bufferSize = size;
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
