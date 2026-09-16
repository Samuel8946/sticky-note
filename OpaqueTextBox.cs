using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

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
/// frame. A reentrancy flag stops the write-back from generating another WM_PAINT that
/// would recurse into this method.
///
/// Window DCs are clipped. A full-client BitBlt still "succeeds" when only a sub-rect is
/// visible, and stamping the leftover buffer pixels would wipe glyphs in the occluded
/// strip. Simple clips copy and restamp that rect 1:1 (nearest-neighbor, no bilinear
/// fringe). Complex clips apply the system visible region (GetRandomRgn SYSRGN via GetDC,
/// mapped into client space) so holes left by overlapping windows are not filled.
///
/// A grow is handled up front rather than by erasing: newly exposed pixels are filled opaque
/// through GDI+ as the control resizes, so they never pass through an alpha-0 state.
///
/// The text caret is a second, independent problem. Windows blinks it by XOR-drawing
/// straight to the window's DC, which never goes through WM_PAINT. Capturing while that
/// inverted caret is on-screen would bake the inversion in as permanent opaque content.
/// HideCaret around the copy — only when this control owns the caret — so a focused
/// capture sees the true glyphs, not a blink frame.
/// </summary>
internal sealed class OpaqueTextBox : TextBox
{
    private const int WM_PAINT = 0x000F;

    private Bitmap? _buffer;
    private Graphics? _bufferGraphics;
    private Size _bufferSize;
    private bool _repairing;
    private bool _priming;
    private Size _primedSize;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WM_PAINT)
            RepairAlpha();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        PrimeGrownEdges();
    }

    /// <summary>
    /// Paints pixels exposed by a grow opaque, immediately.
    ///
    /// A grow leaves the new strip never-painted, and the obvious way to give it a background
    /// (WM_ERASEBKGND, via RDW_ERASE) fills it with plain GDI, which means alpha 0 until the
    /// next paint repair -- the same wallpaper flash this class exists to prevent, just along
    /// the new edge, and repeated on every mouse-move tick of a resize. Filling here with GDI+
    /// instead writes the alpha byte, so the strip is opaque from the moment it exists. WM_SIZE
    /// is dispatched inside SetWindowPos, so this lands before the frame can be presented.
    /// </summary>
    private void PrimeGrownEdges()
    {
        Size previous = _primedSize;
        Size size = ClientSize;

        if (!IsHandleCreated || !Visible || size.Width <= 0 || size.Height <= 0)
        {
            // Don't record a size we never primed; a later layout must still see a delta.
            return;
        }

        bool widened = AlphaClip.TryGetWidthStrip(previous, size, out Rectangle widthStrip);
        bool heightened = AlphaClip.TryGetHeightStrip(previous, size, out Rectangle heightStrip);
        if (!widened && !heightened)
        {
            _primedSize = size;
            return;
        }

        // FillRectangle via FromHwnd can generate a paint. Mark priming so RepairAlpha
        // does not BitBlt mid-fill against a half-filled surface. Use a separate flag from
        // _repairing so a nested SizeChanged during repair still primes the new strips.
        if (_priming)
            return;

        _priming = true;
        try
        {
            using Graphics g = Graphics.FromHwnd(Handle);
            g.CompositingMode = CompositingMode.SourceCopy;
            using var brush = new SolidBrush(Color.FromArgb(255, BackColor));

            if (widened)
                g.FillRectangle(brush, widthStrip);
            if (heightened)
                g.FillRectangle(brush, heightStrip);

            _primedSize = size;
        }
        catch (Exception ex) when (IsGdiFailure(ex))
        {
            // Leave _primedSize alone so the next size change still tries to prime.
            Debug.WriteLine($"OpaqueTextBox prime skipped: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _priming = false;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // A recreated HWND has never-painted pixels; re-prime from empty so the whole
        // client is filled opaque on first layout rather than waiting for a later grow.
        _primedSize = Size.Empty;
        PrimeGrownEdges();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        // Primes are skipped while hidden. When shown again, force a full opaque fill so
        // any size change that happened off-screen is not left at alpha 0. Skip when the
        // size was already primed this cycle (HandleCreated often runs first).
        if (Visible && _primedSize != ClientSize)
        {
            _primedSize = Size.Empty;
            PrimeGrownEdges();
        }
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
        // Skip while priming: the fill is already writing opaque pixels, and a BitBlt now
        // would race a half-filled strip.
        if (_repairing || _priming)
            return;

        _repairing = true;
        try
        {
            try
            {
                RepairAlphaCore();
            }
            catch (Exception ex) when (IsGdiFailure(ex))
            {
                // One dropped paint repair looks washed out for a frame. Crashing the note does not.
                Debug.WriteLine($"OpaqueTextBox repair skipped: {ex.GetType().Name}: {ex.Message}");
            }
        }
        finally
        {
            _repairing = false;
        }
    }

    private void RepairAlphaCore()
    {
        if (!IsHandleCreated || !Visible)
            return;

        Size size = ClientSize;
        if (size.Width <= 0 || size.Height <= 0)
            return;

        if (!EnsureBuffer(size))
            return;

        // GetDC (not GDI+ GetHdc) so GetClipBox / GetRandomRgn(SYSRGN) see the real window
        // visible region. GDI+ GetHdc is unreliable for system-clip queries.
        IntPtr source = Native.GetDC(Handle);
        if (source == IntPtr.Zero)
            return;

        IntPtr clipRgn = IntPtr.Zero;
        bool haveVisibleRegion = false;
        bool caretHidden = false;

        try
        {
            int complexity = Native.GetClipBox(source, out Native.RECT clip);
            if (!AlphaClip.TryGetStampRect(
                    complexity, clip.Left, clip.Top, clip.Right, clip.Bottom, size, out Rectangle stamp))
                return;

            clipRgn = Native.CreateRectRgn(0, 0, 0, 0);
            if (clipRgn != IntPtr.Zero)
                haveVisibleRegion = TryMapSystemVisibleRegion(source, clipRgn);

            if (AlphaClip.ShouldSkipComplexWithoutVisibleRegion(complexity, haveVisibleRegion))
                return;

            // HideCaret only succeeds when this thread owns the caret. Header-drag paints
            // don't, and calling it there is a no-op that used to look like a guarantee.
            caretHidden = Focused && Native.HideCaret(Handle);

            IntPtr destination = IntPtr.Zero;
            bool copied;
            try
            {
                destination = _bufferGraphics!.GetHdc();
                copied = Native.BitBlt(
                    destination, stamp.X, stamp.Y, stamp.Width, stamp.Height,
                    source, stamp.X, stamp.Y, Native.SRCCOPY);
            }
            finally
            {
                if (destination != IntPtr.Zero)
                    _bufferGraphics!.ReleaseHdc(destination);
            }

            // Source DC is no longer needed for the GDI+ write-back.
            Native.ReleaseDC(Handle, source);
            source = IntPtr.Zero;

            if (!copied)
                return;

            ForceOpaque(_buffer!, stamp);

            using Graphics target = Graphics.FromHwnd(Handle);

            // 1:1 copy. Bilinear sampling would read uninitialized pixels just outside
            // the stamp (and region holes) and smear them into the visible edge.
            target.InterpolationMode = InterpolationMode.NearestNeighbor;
            target.PixelOffsetMode = PixelOffsetMode.None;
            target.CompositingMode = CompositingMode.SourceCopy;

            if (AlphaClip.ShouldApplyVisibleRegionClip(haveVisibleRegion) && clipRgn != IntPtr.Zero)
            {
                using var region = Region.FromHrgn(clipRgn);
                target.SetClip(region, CombineMode.Replace);
            }

            target.DrawImage(_buffer!, stamp, stamp, GraphicsUnit.Pixel);
        }
        finally
        {
            if (source != IntPtr.Zero)
                Native.ReleaseDC(Handle, source);
            if (clipRgn != IntPtr.Zero)
                Native.DeleteObject(clipRgn);
            if (caretHidden)
                Native.ShowCaret(Handle);
        }
    }

    /// <summary>
    /// Copies the DC's system visible region into <paramref name="clipRgn"/> and maps it from
    /// screen coordinates into this control's client space. Returns false when the region is
    /// missing, empty, or could not be mapped.
    /// </summary>
    private bool TryMapSystemVisibleRegion(IntPtr sourceDc, IntPtr clipRgn)
    {
        // GetClipRgn only returns an application SelectClipRgn. The COMPLEXREGION that
        // matters here is the system visible region from overlapping windows.
        if (Native.GetRandomRgn(sourceDc, clipRgn, Native.SYSRGN) != 1)
            return false;

        var origin = new Native.POINT { X = 0, Y = 0 };
        if (!Native.ClientToScreen(Handle, ref origin))
            return false;

        // OffsetRgn returns ERROR (0) on failure.
        return Native.OffsetRgn(clipRgn, -origin.X, -origin.Y) != 0;
    }

    /// <summary>
    /// GDI/GDI+ failures that mean "this frame did not get repaired", never "the app is broken".
    /// </summary>
    private static bool IsGdiFailure(Exception ex) =>
        ex is ExternalException
            or ArgumentException
            or ObjectDisposedException
            or InvalidOperationException
            or Win32Exception;

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
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException)
        {
            return false;
        }

        try
        {
            graphics = Graphics.FromImage(bitmap);
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or ExternalException)
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

    private static unsafe void ForceOpaque(Bitmap bitmap, Rectangle area)
    {
        if (area.Width <= 0 || area.Height <= 0)
            return;

        BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadWrite,
            PixelFormat.Format32bppArgb);

        try
        {
            int left = Math.Clamp(area.Left, 0, data.Width);
            int top = Math.Clamp(area.Top, 0, data.Height);
            int right = Math.Clamp(area.Right, 0, data.Width);
            int bottom = Math.Clamp(area.Bottom, 0, data.Height);

            for (int y = top; y < bottom; y++)
            {
                byte* row = (byte*)data.Scan0 + (y * data.Stride);
                for (int x = left; x < right; x++)
                    row[(x * 4) + 3] = 255;
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
