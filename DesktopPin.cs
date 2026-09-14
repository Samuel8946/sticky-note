namespace StickyNote;

/// <summary>
/// Locates the Explorer window that hosts the desktop icons, which is where the note lives.
///
/// Of the several windows that make up the desktop, SHELLDLL_DefView is the only viable parent.
/// Reparenting into Progman or the wallpaper-layer WorkerW leaves the window composited but
/// never painted, so it shows up as a black rectangle. As a WS_CHILD of SHELLDLL_DefView the
/// note paints normally, draws above the icons, takes mouse and keyboard input, and -- because
/// it is no longer a top-level window -- is ignored by Show Desktop (Win+D) and the taskbar.
/// </summary>
internal static class DesktopPin
{
    public static IntPtr Find()
    {
        IntPtr progman = Native.FindWindow("Progman", null);

        if (progman != IntPtr.Zero)
        {
            IntPtr defView = Native.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero)
                return defView;
        }

        // Wallpaper apps can move the icon view out to a WorkerW, so fall back to a scan.
        IntPtr found = IntPtr.Zero;
        Native.EnumWindows((hwnd, _) =>
        {
            IntPtr defView = Native.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView == IntPtr.Zero)
                return true;

            found = defView;
            return false;
        }, IntPtr.Zero);

        return found;
    }
}
