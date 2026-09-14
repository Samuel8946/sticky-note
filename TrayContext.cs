using System.Diagnostics;
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

    /// <summary>
    /// Reuses the icon baked into the executable (see <c>ApplicationIcon</c> in the project
    /// file) so the tray glyph and the exe's own icon always match, with no extra embedded
    /// resource needed to keep the app a single self-contained file.
    /// </summary>
    private static Icon CreateNoteIcon()
    {
        string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
        return Icon.ExtractAssociatedIcon(exePath) is Icon fromExe ? fromExe : SystemIcons.Application;
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
