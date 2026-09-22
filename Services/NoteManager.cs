using System.Windows;
using StickyNoteV2.Models;
using StickyNoteV2.Views;

namespace StickyNoteV2.Services;

public class NoteManager
{
    private readonly NoteStore _store;
    private readonly List<Note> _notes = new();
    private readonly Dictionary<Guid, NoteWindow> _windows = new();

    public bool HasNotes => _notes.Count > 0;

    public NoteManager(NoteStore store)
    {
        _store = store;
    }

    public void LoadNotes()
    {
        var notes = _store.Load();
        _notes.Clear();
        _notes.AddRange(notes);

        // Create windows for each note
        foreach (var note in _notes)
        {
            CreateWindowForNote(note);
        }
    }

    public void SaveNotes()
    {
        _store.Flush();
    }

    public Note CreateNote(double? x = null, double? y = null)
    {
        var note = new Note
        {
            Id = Guid.NewGuid(),
            X = x ?? GetNewNoteX(),
            Y = y ?? GetNewNoteY(),
            Width = 250,
            Height = 250,
            Color = NoteColors.Yellow,
            Created = DateTime.Now,
            Modified = DateTime.Now
        };

        _notes.Add(note);
        CreateWindowForNote(note);
        _store.SaveDebounced(_notes);

        return note;
    }

    private double GetNewNoteX()
    {
        // Cascade new notes
        var baseX = 100.0;
        var offset = (_notes.Count % 10) * 30;
        return Math.Min(baseX + offset, SystemParameters.WorkArea.Width - 300);
    }

    private double GetNewNoteY()
    {
        var baseY = 100.0;
        var offset = (_notes.Count % 10) * 30;
        return Math.Min(baseY + offset, SystemParameters.WorkArea.Height - 300);
    }

    private void CreateWindowForNote(Note note)
    {
        var window = new NoteWindow(note);
        
        window.NoteChanged += OnNoteChanged;
        window.NoteDeleted += OnNoteDeleted;
        window.NewNoteRequested += () => CreateNote();
        window.Closed += (s, e) => _windows.Remove(note.Id);

        _windows[note.Id] = window;
        window.EnsureOnScreen();
        window.Show();
    }

    private void OnNoteChanged(Note note)
    {
        _store.SaveDebounced(_notes);
    }

    private void OnNoteDeleted(Note note)
    {
        _notes.Remove(note);
        _windows.Remove(note.Id);
        _store.SaveDebounced(_notes);
    }

    public void ShowAllNotes()
    {
        foreach (var window in _windows.Values)
        {
            window.Show();
            window.Activate();
        }
    }

    public void HideAllNotes()
    {
        foreach (var window in _windows.Values)
        {
            window.Hide();
        }
    }

    public void CloseAllWindows()
    {
        foreach (var window in _windows.Values.ToList())
        {
            window.Close();
        }
        _windows.Clear();
    }
}
