using System.Drawing;
using WinForms = System.Windows.Forms;
using WpfApp = System.Windows.Application;

namespace StickyNoteV2.Services;

public class TrayManager : IDisposable
{
    private readonly WinForms.NotifyIcon _trayIcon;
    private readonly NoteManager _noteManager;
    private readonly WinForms.ToolStripMenuItem _startupMenuItem;
    private bool _disposed;

    public TrayManager(NoteManager noteManager)
    {
        _noteManager = noteManager;

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "StickNote",
            Visible = true
        };

        // Create context menu
        var contextMenu = new WinForms.ContextMenuStrip();

        var newNoteItem = new WinForms.ToolStripMenuItem("New Note");
        newNoteItem.Click += (s, e) => WpfApp.Current.Dispatcher.Invoke(() => _noteManager.CreateNote());

        var separator1 = new WinForms.ToolStripSeparator();

        var showAllItem = new WinForms.ToolStripMenuItem("Show All Notes");
        showAllItem.Click += (s, e) => WpfApp.Current.Dispatcher.Invoke(() => _noteManager.ShowAllNotes());

        var hideAllItem = new WinForms.ToolStripMenuItem("Hide All Notes");
        hideAllItem.Click += (s, e) => WpfApp.Current.Dispatcher.Invoke(() => _noteManager.HideAllNotes());

        var separator2 = new WinForms.ToolStripSeparator();

        _startupMenuItem = new WinForms.ToolStripMenuItem("Start with Windows")
        {
            Checked = StartupManager.IsEnabled(),
            CheckOnClick = true
        };
        _startupMenuItem.Click += (s, e) =>
        {
            if (_startupMenuItem.Checked)
                StartupManager.Enable();
            else
                StartupManager.Disable();
        };

        var separator3 = new WinForms.ToolStripSeparator();

        var exitItem = new WinForms.ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) =>
        {
            WpfApp.Current.Dispatcher.Invoke(() =>
            {
                _noteManager.SaveNotes();
                WpfApp.Current.Shutdown();
            });
        };

        contextMenu.Items.Add(newNoteItem);
        contextMenu.Items.Add(separator1);
        contextMenu.Items.Add(showAllItem);
        contextMenu.Items.Add(hideAllItem);
        contextMenu.Items.Add(separator2);
        contextMenu.Items.Add(_startupMenuItem);
        contextMenu.Items.Add(separator3);
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = contextMenu;

        // Double-click creates new note
        _trayIcon.DoubleClick += (s, e) => 
            WpfApp.Current.Dispatcher.Invoke(() => _noteManager.CreateNote());
    }

    private Icon CreateDefaultIcon()
    {
        // Try to load the custom icon
        try
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var icoPath = System.IO.Path.Combine(appDir, "app.ico");
            if (System.IO.File.Exists(icoPath))
            {
                return new Icon(icoPath);
            }
            
            var pngPath = System.IO.Path.Combine(appDir, "sticky icon.png");
            if (System.IO.File.Exists(pngPath))
            {
                var bitmap = new Bitmap(pngPath);
                return Icon.FromHandle(bitmap.GetHicon());
            }
        }
        catch { }

        // Fallback: Create a simple sticky note icon programmatically
        var fallbackBitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(fallbackBitmap))
        {
            g.Clear(Color.Transparent);
            
            using (var brush = new SolidBrush(Color.FromArgb(255, 245, 157)))
            {
                g.FillRectangle(brush, 2, 2, 28, 28);
            }
            
            using (var pen = new Pen(Color.FromArgb(200, 200, 100), 1))
            {
                g.DrawRectangle(pen, 2, 2, 27, 27);
            }
            
            using (var pen = new Pen(Color.FromArgb(100, 100, 100), 1))
            {
                g.DrawLine(pen, 6, 10, 24, 10);
                g.DrawLine(pen, 6, 15, 24, 15);
                g.DrawLine(pen, 6, 20, 18, 20);
            }
        }
        
        return Icon.FromHandle(fallbackBitmap.GetHicon());
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _disposed = true;
        }
    }
}
