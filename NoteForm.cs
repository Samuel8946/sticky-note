using System.Drawing.Drawing2D;

namespace StickyNote;

/// <summary>
/// The note itself: a borderless tool window reparented into Explorer's desktop window.
/// Because it lives inside the desktop it has no taskbar button, survives Win+D, and never
/// floats over other applications. It also means the window has no real title bar or sizing
/// border, so dragging and resizing are handled by hand below.
/// </summary>
internal sealed class NoteForm : Form
{
    private const int Border = 5;
    private const int HeaderHeight = 26;
    private const int MinWidth = 180;
    private const int MinHeight = 120;
    private const int HotkeyId = 0xB00C;

    private enum Zone
    {
        None, Move, Left, Right, Top, Bottom,
        TopLeft, TopRight, BottomLeft, BottomRight
    }

    private readonly NoteSettings _settings;
    private readonly OpaqueTextBox _editor;
    private readonly Font _headerFont;
    private readonly System.Windows.Forms.Timer _autoSave;
    private readonly System.Windows.Forms.Timer _zKeeper;
    private readonly System.Windows.Forms.Timer _attachRetry;

    private Font? _editorFont;

    private readonly uint _taskbarCreatedMessage = Native.RegisterWindowMessage("TaskbarCreated");

    private IntPtr _desktopHost = IntPtr.Zero;
    private int _attachAttempts;

    private Zone _dragZone = Zone.None;
    private Point _dragOrigin;
    private Rectangle _dragStartBounds;

    private int _hotButton = -1;
    private int _pressedButton = -1;

    /// <summary>Raised when the user clicks the header's close glyph, so the tray can update its state.</summary>
    public event EventHandler? HideRequested;

    public NoteForm(NoteSettings settings)
    {
        _settings = settings;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        MinimumSize = new Size(MinWidth, MinHeight);
        KeyPreview = true;
        DoubleBuffered = true;
        ResizeRedraw = true;
        Padding = new Padding(Border, HeaderHeight, Border, Border);
        Text = "Sticky Note";

        _headerFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);

        _editor = new OpaqueTextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AcceptsTab = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            Text = NoteStore.LoadNote(),
        };
        Controls.Add(_editor);

        _autoSave = new System.Windows.Forms.Timer { Interval = 1000 };
        _autoSave.Tick += (_, _) =>
        {
            _autoSave.Stop();
            NoteStore.SaveNote(_editor.Text);
        };

        // Debounce: only write once the typing pauses.
        _editor.TextChanged += (_, _) =>
        {
            _autoSave.Stop();
            _autoSave.Start();
        };

        // Explorer occasionally raises the icon view above us; nudge back to the front of the
        // desktop's children when that happens.
        _zKeeper = new System.Windows.Forms.Timer { Interval = 1500 };
        _zKeeper.Tick += (_, _) => KeepAttached();
        _zKeeper.Start();

        // At logon Progman may not exist yet, so keep trying until the desktop is ready.
        _attachRetry = new System.Windows.Forms.Timer { Interval = 1000 };
        _attachRetry.Tick += (_, _) =>
        {
            if (AttachToDesktop() || ++_attachAttempts > 90)
                _attachRetry.Stop();
        };

        ApplyAppearance();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW;   // keeps it out of Alt+Tab
            cp.ExStyle &= ~Native.WS_EX_APPWINDOW;

            // Not WS_EX_LAYERED: a layered child of the desktop is not composited at all.

            // Deliberately not parented here. A window created directly as a child of the
            // desktop never gets its own render surface, and paints semi-transparently with
            // the wallpaper bleeding through. It has to be born top-level and adopted after.
            return cp;
        }
    }

    public bool IsLocked
    {
        get => _settings.Locked;
        set
        {
            _settings.Locked = value;
            NoteStore.SaveSettings(_settings);
            Invalidate();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.RegisterHotKey(Handle, HotkeyId, Native.MOD_WIN | Native.MOD_SHIFT | Native.MOD_NOREPEAT, (uint)Keys.N);
    }

    /// <summary>
    /// Creates the window and moves it into the desktop. This has to happen via Show() rather
    /// than by touching Handle: WinForms parks handles created while hidden inside an internal
    /// helper window, and the later Show() would drag the note back out of the desktop with it.
    /// </summary>
    public void Bootstrap()
    {
        Rectangle target = SavedBounds();

        // Create it offscreen, so the unavoidable top-level phase is never visible.
        base.Bounds = new Rectangle(-32000, -32000, target.Width, target.Height);
        Show();

        if (!AttachToDesktop())
        {
            _attachAttempts = 0;
            _attachRetry.Start();
        }

        if (_settings.Visible)
            SetScreenBounds(target);
        else
            Hide();
    }

    private Rectangle SavedBounds() => EnsureReachable(new Rectangle(
        _settings.X, _settings.Y,
        Math.Max(MinWidth, _settings.Width),
        Math.Max(MinHeight, _settings.Height)));

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Native.UnregisterHotKey(Handle, HotkeyId);
        base.OnHandleDestroyed(e);
    }

    /// <summary>Reparents the note into the desktop. Safe to call repeatedly.</summary>
    public bool AttachToDesktop()
    {
        if (!IsHandleCreated)
            return false;

        IntPtr host = DesktopPin.Find();
        if (host == IntPtr.Zero)
            return false;

        Rectangle screenBounds = GetScreenBounds();

        // WS_CHILD is what makes this work. Without it the window is reparented but never
        // painted, and shows up on the desktop as a black rectangle.
        int style = Native.GetWindowLong(Handle, Native.GWL_STYLE);
        Native.SetWindowLong(Handle, Native.GWL_STYLE,
            (style & ~Native.WS_POPUP) | Native.WS_CHILD | Native.WS_CLIPSIBLINGS);

        Native.SetParent(Handle, host);

        if (Native.GetAncestor(Handle, Native.GA_PARENT) != host)
            return false;

        _desktopHost = host;

        // Coordinates become parent-relative the moment we are reparented, so put the note
        // back where it was in screen space.
        SetScreenBounds(screenBounds, Native.SWP_FRAMECHANGED);
        KeepInFront();
        ForceRedraw();
        return true;
    }

    /// <summary>
    /// Reparenting leaves the window holding a stale surface with no pending paint, so it keeps
    /// showing whatever pixels happened to be there. Nothing else invalidates it, so do it here.
    /// </summary>
    private void ForceRedraw()
    {
        if (!IsHandleCreated)
            return;

        Native.RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero,
            Native.RDW_INVALIDATE | Native.RDW_ERASE | Native.RDW_FRAME |
            Native.RDW_ALLCHILDREN | Native.RDW_UPDATENOW);
    }

    public void Reattach()
    {
        _attachAttempts = 0;
        if (!AttachToDesktop())
            _attachRetry.Start();
    }

    /// <summary>
    /// Explorer rebuilds the icon view for things like a desktop refresh or a resolution
    /// change, which silently orphans us. Re-home the note when that happens.
    /// </summary>
    private void KeepAttached()
    {
        if (_desktopHost == IntPtr.Zero || !IsHandleCreated)
            return;

        if (Native.GetAncestor(Handle, Native.GA_PARENT) != _desktopHost || !Native.IsWindow(_desktopHost))
            Reattach();
        else
            KeepInFront();
    }

    private void KeepInFront()
    {
        if (_desktopHost == IntPtr.Zero || !IsHandleCreated || !Visible)
            return;

        // A null previous sibling means nothing in the desktop is drawn above us already.
        // Otherwise the icon list has come back on top and we need to get above it again.
        if (Native.GetWindow(Handle, Native.GW_HWNDPREV) == IntPtr.Zero)
            return;

        Native.SetWindowPos(Handle, Native.HWND_TOP, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    /// <summary>Pulls the note forward and puts the caret in it, even though Alt+Tab cannot reach it.</summary>
    public void FocusNote()
    {
        if (!Visible)
            ShowNote();

        KeepInFront();

        IntPtr foreground = Native.GetForegroundWindow();
        uint foregroundThread = Native.GetWindowThreadProcessId(foreground, out _);
        uint currentThread = Native.GetCurrentThreadId();
        bool attached = foregroundThread != currentThread &&
                        Native.AttachThreadInput(foregroundThread, currentThread, true);

        // SetForegroundWindow only accepts top-level windows, and ours is now a child of the
        // desktop, so raise the desktop itself and then put the caret in the note.
        IntPtr root = Native.GetAncestor(Handle, Native.GA_ROOT);
        if (root != IntPtr.Zero)
            Native.SetForegroundWindow(root);

        Native.BringWindowToTop(Handle);
        Native.SetFocus(_editor.Handle);

        if (attached)
            Native.AttachThreadInput(foregroundThread, currentThread, false);
    }

    public void ShowNote()
    {
        Rectangle target = Visible ? GetScreenBounds() : SavedBounds();

        Show();

        // Showing can pull the window back out of the desktop, and WinForms re-applies its own
        // Bounds, which are parent-relative once we are inside it. Redo both.
        AttachToDesktop();
        SetScreenBounds(EnsureReachable(target));

        _settings.Visible = true;
        NoteStore.SaveSettings(_settings);
        KeepInFront();
        ForceRedraw();
    }

    public void HideNote()
    {
        Hide();
        _settings.Visible = false;
        NoteStore.SaveSettings(_settings);
    }

    /// <summary>Writes the note body and geometry immediately, bypassing the autosave delay.</summary>
    public void FlushNow()
    {
        _autoSave.Stop();
        NoteStore.SaveNote(_editor.Text);
        CaptureBounds();
        NoteStore.SaveSettings(_settings);
    }

    public void ApplyAppearance()
    {
        Color note = Color.FromArgb(_settings.NoteColor);
        Color text = Color.FromArgb(_settings.TextColor);

        BackColor = note;
        _editor.BackColor = note;
        _editor.ForeColor = text;

        Font newFont;
        try
        {
            newFont = new Font(_settings.FontFamily, _settings.FontSize, (FontStyle)_settings.FontStyle);
        }
        catch
        {
            newFont = new Font("Segoe UI", 10.5f);
        }

        _editor.Font = newFont;
        _editorFont?.Dispose();
        _editorFont = newFont;

        Invalidate();
    }

    public void ChooseFont()
    {
        using var dialog = new FontDialog { Font = _editor.Font, ShowEffects = false };

        if (!TryShowCommonDialog(dialog, "choosing a font"))
            return;

        _settings.FontFamily = dialog.Font.FontFamily.Name;
        _settings.FontSize = dialog.Font.SizeInPoints;
        _settings.FontStyle = (int)dialog.Font.Style;
        NoteStore.SaveSettings(_settings);
        ApplyAppearance();
    }

    public void ChooseColor(bool background)
    {
        using var dialog = new ColorDialog
        {
            FullOpen = true,
            Color = Color.FromArgb(background ? _settings.NoteColor : _settings.TextColor),
        };

        if (!TryShowCommonDialog(dialog, "choosing a color"))
            return;

        if (background)
            _settings.NoteColor = dialog.Color.ToArgb();
        else
            _settings.TextColor = dialog.Color.ToArgb();

        NoteStore.SaveSettings(_settings);
        ApplyAppearance();
    }

    /// <summary>
    /// Runs a CommonDialog (FontDialog, ColorDialog, ...) with a real top-level owner window.
    ///
    /// Called with no owner, CommonDialog.ShowDialog() resolves one via GetActiveWindow().
    /// NoteForm is a WS_CHILD of Explorer's desktop rather than a normal top-level window, so it
    /// can never legitimately be "the active window" -- and after a tray-menu click there may be
    /// no valid top-level window to fall back to at all. FontDialog specifically then builds its
    /// DPI/Graphics context off that invalid handle, and Font.ToLogFont throws inside native
    /// GDI+ interop ("Parameter is not valid"). A throwaway, offscreen, invisible top-level
    /// window sidesteps the whole problem. The try/catch is a second line of defence: a crashed
    /// dialog should never take down an always-running background note.
    /// </summary>
    private static bool TryShowCommonDialog(CommonDialog dialog, string action)
    {
        using var owner = new Form
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            Size = new Size(1, 1),
            Opacity = 0,
        };

        try
        {
            owner.Show();
            return dialog.ShowDialog(owner) == DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Something went wrong {action}.\n\n{ex.Message}",
                "Sticky Note", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    // ---- geometry -------------------------------------------------------------------

    private Rectangle GetScreenBounds()
    {
        if (!IsHandleCreated)
            return new Rectangle(_settings.X, _settings.Y, _settings.Width, _settings.Height);

        Native.GetWindowRect(Handle, out Native.RECT r);
        return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
    }

    private void SetScreenBounds(Rectangle bounds, uint extraFlags = 0)
    {
        int x = bounds.X;
        int y = bounds.Y;

        // Inside the desktop window, SetWindowPos takes parent-client coordinates. The desktop
        // spans the whole virtual screen, whose origin can be negative on multi-monitor setups.
        if (_desktopHost != IntPtr.Zero)
        {
            var point = new Native.POINT { X = bounds.X, Y = bounds.Y };
            if (Native.ScreenToClient(_desktopHost, ref point))
            {
                x = point.X;
                y = point.Y;
            }
        }

        Native.SetWindowPos(Handle, IntPtr.Zero, x, y, bounds.Width, bounds.Height,
            Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | extraFlags);

        // Moving inside the desktop does not reliably schedule a paint either.
        ForceRedraw();
    }

    /// <summary>Drags a note back into view if the monitor it lived on is gone.</summary>
    private static Rectangle EnsureReachable(Rectangle bounds)
    {
        Rectangle virtualScreen = SystemInformation.VirtualScreen;

        // Require a usable sliver of the header to remain grabbable.
        var grabbable = new Rectangle(bounds.X, bounds.Y, Math.Min(bounds.Width, 120), HeaderHeight);
        if (virtualScreen.IntersectsWith(grabbable))
            return bounds;

        Rectangle primary = Screen.PrimaryScreen?.WorkingArea ?? virtualScreen;
        return new Rectangle(primary.X + 140, primary.Y + 140, bounds.Width, bounds.Height);
    }

    private void CaptureBounds()
    {
        Rectangle bounds = GetScreenBounds();
        _settings.X = bounds.X;
        _settings.Y = bounds.Y;
        _settings.Width = bounds.Width;
        _settings.Height = bounds.Height;
    }

    // ---- header chrome --------------------------------------------------------------

    private Rectangle ButtonBounds(int index)
    {
        const int w = 24;
        const int h = 18;
        int right = ClientSize.Width - Border - (index * (w + 2));
        return new Rectangle(right - w, (HeaderHeight - h) / 2, w, h);
    }

    private int ButtonAt(Point client)
    {
        if (client.Y >= HeaderHeight)
            return -1;

        for (int i = 0; i < 2; i++)
        {
            if (ButtonBounds(i).Contains(client))
                return i;
        }

        return -1;
    }

    private Zone ZoneAt(Point client)
    {
        bool left = client.X < Border;
        bool right = client.X >= ClientSize.Width - Border;
        bool top = client.Y < Border;
        bool bottom = client.Y >= ClientSize.Height - Border;

        if (top && left) return Zone.TopLeft;
        if (top && right) return Zone.TopRight;
        if (bottom && left) return Zone.BottomLeft;
        if (bottom && right) return Zone.BottomRight;
        if (left) return Zone.Left;
        if (right) return Zone.Right;
        if (top) return Zone.Top;
        if (bottom) return Zone.Bottom;
        if (client.Y < HeaderHeight) return Zone.Move;

        return Zone.None;
    }

    private static Cursor CursorFor(Zone zone) => zone switch
    {
        Zone.Left or Zone.Right => Cursors.SizeWE,
        Zone.Top or Zone.Bottom => Cursors.SizeNS,
        Zone.TopLeft or Zone.BottomRight => Cursors.SizeNWSE,
        Zone.TopRight or Zone.BottomLeft => Cursors.SizeNESW,
        Zone.Move => Cursors.SizeAll,
        _ => Cursors.Default,
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color note = Color.FromArgb(_settings.NoteColor);
        Color text = Color.FromArgb(_settings.TextColor);
        Color header = Shade(note, 0.90f);
        Color line = Shade(note, 0.72f);

        var headerRect = new Rectangle(0, 0, ClientSize.Width, HeaderHeight);
        using (var brush = new SolidBrush(header))
            g.FillRectangle(brush, headerRect);

        using (var pen = new Pen(line))
        {
            g.DrawLine(pen, 0, HeaderHeight - 1, ClientSize.Width, HeaderHeight - 1);
            g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        string title = _settings.Locked ? "Note  (locked)" : "Note";
        var titleRect = new Rectangle(Border + 2, 0, ClientSize.Width - 70, HeaderHeight);
        TextRenderer.DrawText(g, title, _headerFont, titleRect,
            Blend(text, header, 0.35f),
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        DrawHeaderButton(g, 0, text, header);   // close
        DrawHeaderButton(g, 1, text, header);   // menu
    }

    private void DrawHeaderButton(Graphics g, int index, Color text, Color header)
    {
        Rectangle bounds = ButtonBounds(index);

        if (_hotButton == index)
        {
            using var hover = new SolidBrush(Shade(header, _pressedButton == index ? 0.82f : 0.93f));
            g.FillRectangle(hover, bounds);
        }

        using var pen = new Pen(Blend(text, header, 0.2f), 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        int cx = bounds.X + bounds.Width / 2;
        int cy = bounds.Y + bounds.Height / 2;

        if (index == 0)
        {
            g.DrawLine(pen, cx - 4, cy - 4, cx + 4, cy + 4);
            g.DrawLine(pen, cx + 4, cy - 4, cx - 4, cy + 4);
        }
        else
        {
            for (int i = -1; i <= 1; i++)
                g.DrawLine(pen, cx - 5, cy + (i * 4), cx + 5, cy + (i * 4));
        }
    }

    private static Color Shade(Color color, float factor) => Color.FromArgb(
        color.A,
        (int)Math.Clamp(color.R * factor, 0, 255),
        (int)Math.Clamp(color.G * factor, 0, 255),
        (int)Math.Clamp(color.B * factor, 0, 255));

    private static Color Blend(Color a, Color b, float amountOfB) => Color.FromArgb(
        255,
        (int)(a.R + ((b.R - a.R) * amountOfB)),
        (int)(a.G + ((b.G - a.G) * amountOfB)),
        (int)(a.B + ((b.B - a.B) * amountOfB)));

    // ---- move and resize ------------------------------------------------------------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
            return;

        int button = ButtonAt(e.Location);
        if (button >= 0)
        {
            _pressedButton = button;
            Capture = true;
            Invalidate(ButtonBounds(button));
            return;
        }

        if (_settings.Locked)
            return;

        Zone zone = ZoneAt(e.Location);
        if (zone == Zone.None)
            return;

        _dragZone = zone;
        _dragOrigin = Cursor.Position;
        _dragStartBounds = GetScreenBounds();
        Capture = true;
        // Drag ForceRedraws every move; pause alpha repair so caret blink cannot be stamped in.
        _editor.SuspendRepairs();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragZone != Zone.None)
        {
            ApplyDrag();
            return;
        }

        int button = ButtonAt(e.Location);
        if (button != _hotButton)
        {
            int previous = _hotButton;
            _hotButton = button;
            if (previous >= 0) Invalidate(ButtonBounds(previous));
            if (button >= 0) Invalidate(ButtonBounds(button));
        }

        Cursor = button >= 0 || _settings.Locked ? Cursors.Default : CursorFor(ZoneAt(e.Location));
    }

    private void ApplyDrag()
    {
        int dx = Cursor.Position.X - _dragOrigin.X;
        int dy = Cursor.Position.Y - _dragOrigin.Y;

        Rectangle start = _dragStartBounds;
        int left = start.Left;
        int top = start.Top;
        int right = start.Right;
        int bottom = start.Bottom;

        switch (_dragZone)
        {
            case Zone.Move:
                left += dx; right += dx; top += dy; bottom += dy;
                break;
            case Zone.Left: left += dx; break;
            case Zone.Right: right += dx; break;
            case Zone.Top: top += dy; break;
            case Zone.Bottom: bottom += dy; break;
            case Zone.TopLeft: left += dx; top += dy; break;
            case Zone.TopRight: right += dx; top += dy; break;
            case Zone.BottomLeft: left += dx; bottom += dy; break;
            case Zone.BottomRight: right += dx; bottom += dy; break;
        }

        if (right - left < MinWidth)
        {
            if (_dragZone is Zone.Left or Zone.TopLeft or Zone.BottomLeft)
                left = right - MinWidth;
            else
                right = left + MinWidth;
        }

        if (bottom - top < MinHeight)
        {
            if (_dragZone is Zone.Top or Zone.TopLeft or Zone.TopRight)
                top = bottom - MinHeight;
            else
                bottom = top + MinHeight;
        }

        SetScreenBounds(Rectangle.FromLTRB(left, top, right, bottom));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_pressedButton >= 0)
        {
            int button = _pressedButton;
            _pressedButton = -1;
            Capture = false;
            Invalidate(ButtonBounds(button));

            if (ButtonAt(e.Location) == button)
            {
                if (button == 0)
                    HideRequested?.Invoke(this, EventArgs.Empty);
                else
                    ContextMenuStrip?.Show(this, e.Location);
            }

            return;
        }

        if (_dragZone == Zone.None)
            return;

        _dragZone = Zone.None;
        Capture = false;
        _editor.ResumeRepairs();
        CaptureBounds();
        NoteStore.SaveSettings(_settings);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (_hotButton < 0)
            return;

        int previous = _hotButton;
        _hotButton = -1;
        Invalidate(ButtonBounds(previous));
    }

    // ---- lifecycle ------------------------------------------------------------------

    protected override void WndProc(ref Message m)
    {
        // Explorer restarted and took our parent window down with it.
        if (m.Msg == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
        {
            base.WndProc(ref m);
            BeginInvoke(Reattach);
            return;
        }

        if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
        {
            FocusNote();
            return;
        }

        if (m.Msg == Native.WM_ENDSESSION)
            FlushNow();

        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        FlushNow();

        // Closing the note should tuck it away, not end the session.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _autoSave.Dispose();
            _zKeeper.Dispose();
            _attachRetry.Dispose();
            _headerFont.Dispose();
            _editorFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}
