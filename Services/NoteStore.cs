using System.IO;
using System.Text.Json;
using StickyNoteV2.Models;

namespace StickyNoteV2.Services;

public class NoteStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private System.Timers.Timer? _saveTimer;
    private List<Note>? _pendingNotes;
    private readonly object _saveLock = new();

    public NoteStore()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StickyNoteV2"
        );

        Directory.CreateDirectory(appDataPath);
        _filePath = Path.Combine(appDataPath, "notes.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public List<Note> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new List<Note>();

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<Note>>(json, _jsonOptions) ?? new List<Note>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading notes: {ex.Message}");
            return new List<Note>();
        }
    }

    public void Save(List<Note> notes)
    {
        lock (_saveLock)
        {
            try
            {
                var json = JsonSerializer.Serialize(notes, _jsonOptions);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving notes: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Debounced save - waits 500ms before saving to avoid excessive disk writes
    /// </summary>
    public void SaveDebounced(List<Note> notes)
    {
        lock (_saveLock)
        {
            _pendingNotes = new List<Note>(notes);

            if (_saveTimer == null)
            {
                _saveTimer = new System.Timers.Timer(500);
                _saveTimer.AutoReset = false;
                _saveTimer.Elapsed += (s, e) =>
                {
                    lock (_saveLock)
                    {
                        if (_pendingNotes != null)
                        {
                            Save(_pendingNotes);
                            _pendingNotes = null;
                        }
                    }
                };
            }

            _saveTimer.Stop();
            _saveTimer.Start();
        }
    }

    /// <summary>
    /// Forces immediate save, used on app shutdown
    /// </summary>
    public void Flush()
    {
        lock (_saveLock)
        {
            _saveTimer?.Stop();
            if (_pendingNotes != null)
            {
                Save(_pendingNotes);
                _pendingNotes = null;
            }
        }
    }
}
