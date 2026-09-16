namespace StickyNote;

/// <summary>
/// Pure clip-box math for the alpha repair. Kept free of GDI so the empty/simple/complex
/// cases can be unit-tested without a window.
/// </summary>
internal static class AlphaClip
{
    public static bool TryGetStampRect(
        int complexity,
        int clipLeft,
        int clipTop,
        int clipRight,
        int clipBottom,
        Size client,
        out Rectangle stamp)
    {
        stamp = Rectangle.Empty;

        // ERROR (0) and NULLREGION: nothing to stamp. SIMPLEREGION is an exact rect.
        // COMPLEXREGION's box is only a bound — callers must also clip the write-back
        // to the system visible region (GetRandomRgn SYSRGN) so holes are not filled.
        if (complexity is not (Native.SIMPLEREGION or Native.COMPLEXREGION))
            return false;

        int left = Math.Max(clipLeft, 0);
        int top = Math.Max(clipTop, 0);
        int right = Math.Min(clipRight, client.Width);
        int bottom = Math.Min(clipBottom, client.Height);
        if (right <= left || bottom <= top)
            return false;

        stamp = Rectangle.FromLTRB(left, top, right, bottom);
        return true;
    }

    /// <summary>
    /// COMPLEXREGION without a usable visible-region handle would restamp buffer junk into
    /// occlusion holes. Skip the write-back rather than punch through glyphs.
    /// </summary>
    public static bool ShouldSkipComplexWithoutVisibleRegion(int complexity, bool haveVisibleRegion) =>
        complexity == Native.COMPLEXREGION && !haveVisibleRegion;

    public static bool ShouldApplyVisibleRegionClip(bool haveVisibleRegion) => haveVisibleRegion;

    /// <summary>
    /// Client pixels exposed by a widening. Client coordinates are relative to the window's
    /// own top-left, so extra width always appears on the right regardless of which screen
    /// edge the user dragged.
    /// </summary>
    public static bool TryGetWidthStrip(Size previous, Size current, out Rectangle strip)
    {
        strip = Rectangle.Empty;

        int from = Math.Max(previous.Width, 0);
        if (current.Width <= from || current.Height <= 0)
            return false;

        strip = Rectangle.FromLTRB(from, 0, current.Width, current.Height);
        return true;
    }

    /// <summary>Client pixels exposed by a heightening; always along the bottom edge.</summary>
    public static bool TryGetHeightStrip(Size previous, Size current, out Rectangle strip)
    {
        strip = Rectangle.Empty;

        int from = Math.Max(previous.Height, 0);
        if (current.Height <= from || current.Width <= 0)
            return false;

        strip = Rectangle.FromLTRB(0, from, current.Width, current.Height);
        return true;
    }
}
