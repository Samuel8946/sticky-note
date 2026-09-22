using StickyNoteV2.Services;
using StickyNoteV2.Views;

namespace StickyNoteV2;

public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;
    private TrayManager? _trayManager;
    private NoteManager? _noteManager;
    private HotkeyService? _hotkeyService;
    private AppSettings _settings = new();

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        const string mutexName = "StickyNoteV2_SingleInstance";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            System.Windows.MessageBox.Show("StickNote is already running!", "StickNote",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        _settings = SettingsStore.Load();

        if (_settings.ShowGuideOnStartup)
        {
            var guide = new GuideWindow(_settings.ShowGuideOnStartup);
            guide.ShowDialog();
            _settings.ShowGuideOnStartup = guide.ShowOnStartup;
            SettingsStore.Save(_settings);
        }

        var noteStore = new NoteStore();
        _noteManager = new NoteManager(noteStore);
        _trayManager = new TrayManager(_noteManager, ShowGuide);
        _hotkeyService = new HotkeyService();
        _hotkeyService.ToggleRequested += () =>
            Dispatcher.Invoke(() => _noteManager.ToggleAllNotes());

        _noteManager.LoadNotes();

        if (!_noteManager.HasNotes)
        {
            _noteManager.CreateNote();
        }
    }

    private void ShowGuide()
    {
        var guide = new GuideWindow(_settings.ShowGuideOnStartup);
        guide.ShowDialog();
        _settings.ShowGuideOnStartup = guide.ShowOnStartup;
        SettingsStore.Save(_settings);
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _noteManager?.SaveNotes();
        _hotkeyService?.Dispose();
        _trayManager?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
