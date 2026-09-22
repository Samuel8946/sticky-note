using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace StickyNoteV2.Services;

/// <summary>
/// Registers a system-wide Alt+` hotkey to toggle note visibility.
/// </summary>
public class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x5A01;
    private const uint ModAlt = 0x0001;
    private const uint VkOem3 = 0xC0; // backtick / tilde key

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private bool _disposed;
    private bool _registered;

    public event Action? ToggleRequested;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("StickNoteHotkey")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = new IntPtr(-3) // HWND_MESSAGE
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        _registered = RegisterHotKey(_source.Handle, HotkeyId, ModAlt, VkOem3);
    }

    public bool IsRegistered => _registered;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            ToggleRequested?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_registered)
        {
            UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }

        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
