using StickyNoteV2.Services;

namespace StickyNoteV2;

public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;
    private TrayManager? _trayManager;
    private NoteManager? _noteManager;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        // Single instance check
        const string mutexName = "StickyNoteV2_SingleInstance";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance is already running
            System.Windows.MessageBox.Show("StickNote is already running!", "StickNote", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Initialize services
        var noteStore = new NoteStore();
        _noteManager = new NoteManager(noteStore);
        _trayManager = new TrayManager(_noteManager);

        // Load existing notes
        _noteManager.LoadNotes();

        // If no notes exist, create a welcome note
        if (!_noteManager.HasNotes)
        {
            _noteManager.CreateNote();
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _noteManager?.SaveNotes();
        _trayManager?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
