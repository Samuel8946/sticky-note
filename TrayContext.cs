using System.Diagnostics;
using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace StickyNote;

/// <summary>
/// Owns the application lifetime. There is no main form, so the note can be hidden without
/// quitting; the tray icon is the only thing that keeps the process alive.
/// </summary>
internal sealed class TrayContext : ApplicationContext
{
    private readonly NoteSettings _settings;
    private readonly NoteForm _note;
    private readonly NotifyIcon _tray;
    private readonly Icon _icon;

    private readonly ToolStripMenuItem _showItem;
    private readonly ToolStripMenuItem _lockItem;
    private readonly ToolStripMenuItem _startupItem;

    public TrayContext()
    {
        _settings = NoteStore.LoadSettings();
        _note = new NoteForm(_settings);
        _note.HideRequested += (_, _) => SetNoteVisible(false);

        _showItem = new ToolStripMenuItem("Show note", null, (_, _) => SetNoteVisible(!_note.Visible))
        {
            CheckOnClick = false,
        };
        _lockItem = new ToolStripMenuItem("Lock position && size", null, (_, _) => _note.IsLocked = !_note.IsLocked);
        _startupItem = new ToolStripMenuItem("Start with Windows", null,
            (_, _) => StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled));

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _showItem,
            new ToolStripMenuItem("Focus note  (Win+Shift+N)", null, (_, _) => _note.FocusNote()),
            new ToolStripSeparator(),
            _lockItem,
            new ToolStripMenuItem("Re-attach to desktop", null, (_, _) => _note.Reattach()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Font...", null, (_, _) => _note.ChooseFont()),
            new ToolStripMenuItem("Note color...", null, (_, _) => _note.ChooseColor(background: true)),
            new ToolStripMenuItem("Text color...", null, (_, _) => _note.ChooseColor(background: false)),
            new ToolStripSeparator(),
            _startupItem,
            new ToolStripMenuItem("Open note folder", null, (_, _) => OpenNoteFolder()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, (_, _) => Quit()),
        });
        menu.Opening += (_, _) => RefreshMenuState();

        _icon = CreateNoteIcon();
        _tray = new NotifyIcon
        {
            Icon = _icon,
            Text = "Sticky Note",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => _note.FocusNote();

        _note.ContextMenuStrip = menu;

        _note.Bootstrap();

        // A note you have to launch by hand is not much of a desktop fixture.
        if (NoteStore.IsFirstRun && !StartupRegistration.IsEnabled)
            StartupRegistration.SetEnabled(true);

        NoteStore.SaveSettings(_settings);

        SystemEvents.SessionEnding += OnSessionEnding;
    }

    private void RefreshMenuState()
    {
        _showItem.Text = _note.Visible ? "Hide note" : "Show note";
        _lockItem.Checked = _note.IsLocked;
        _startupItem.Checked = StartupRegistration.IsEnabled;
    }

    private void SetNoteVisible(bool visible)
    {
        if (visible)
            _note.ShowNote();
        else
            _note.HideNote();
    }

    private static void OpenNoteFolder()
    {
        Directory.CreateDirectory(NoteStore.Folder);
        Process.Start(new ProcessStartInfo(NoteStore.Folder) { UseShellExecute = true });
    }

    private void OnSessionEnding(object sender, SessionEndingEventArgs e) => _note.FlushNow();

    private void Quit()
    {
        _note.FlushNow();
        _tray.Visible = false;
        ExitThread();
    }

    /// <summary>Draws the tray icon at runtime so the app stays a single self-contained file.</summary>
    private static Icon CreateNoteIcon()
    {
        using var bitmap = new Bitmap(32, 32);

        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var body = new Rectangle(4, 3, 24, 26);
            using (var fill = new SolidBrush(Color.FromArgb(255, 243, 176)))
                g.FillRectangle(fill, body);
            using (var edge = new Pen(Color.FromArgb(150, 124, 40), 1.6f))
                g.DrawRectangle(edge, body);
            using (var rule = new Pen(Color.FromArgb(120, 100, 36), 1.5f))
            {
                for (int i = 0; i < 4; i++)
                    g.DrawLine(rule, 9, 10 + (i * 5), 23, 10 + (i * 5));
            }
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            Native.DestroyIcon(handle);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.SessionEnding -= OnSessionEnding;
            _tray.Dispose();
            _icon.Dispose();
            _note.Dispose();
        }

        base.Dispose(disposing);
    }
}
