using System.Text;
using System.Text.Json;

namespace StickyNote;

internal sealed class NoteSettings
{
    public int X { get; set; } = 140;
    public int Y { get; set; } = 140;
    public int Width { get; set; } = 340;
    public int Height { get; set; } = 400;

    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }

    public string FontFamily { get; set; } = "Segoe UI";
    public float FontSize { get; set; } = 10.5f;
    public int FontStyle { get; set; }

    public int NoteColor { get; set; } = unchecked((int)0xFFFFF3B0);
    public int TextColor { get; set; } = unchecked((int)0xFF2B2B22);
}

/// <summary>Reads and writes the note body and its settings under %APPDATA%\StickyNote.</summary>
internal static class NoteStore
{
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickyNote");

    private static string NotePath => Path.Combine(Folder, "note.txt");
    private static string SettingsPath => Path.Combine(Folder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>True when no settings file existed at startup, i.e. this is a fresh install.</summary>
    public static bool IsFirstRun { get; } = !File.Exists(Path.Combine(Folder, "settings.json"));

    public static string LoadNote()
    {
        try
        {
            return File.Exists(NotePath) ? File.ReadAllText(NotePath, Encoding.UTF8) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static void SaveNote(string text)
    {
        try
        {
            WriteAtomic(NotePath, text);
        }
        catch
        {
            // A failed autosave must never take the note down; the next tick tries again.
        }
    }

    public static NoteSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<NoteSettings>(File.ReadAllText(SettingsPath, Encoding.UTF8));
                if (loaded is not null)
                    return loaded;
            }
        }
        catch
        {
            // Fall through to defaults rather than refusing to start on a corrupt file.
        }

        return new NoteSettings();
    }

    public static void SaveSettings(NoteSettings settings)
    {
        try
        {
            WriteAtomic(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
        }
    }

    /// <summary>Write via a temp file so a power loss mid-save cannot truncate the note.</summary>
    private static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(Folder);

        string temp = path + ".tmp";
        File.WriteAllText(temp, content, Utf8NoBom);

        if (File.Exists(path))
            File.Replace(temp, path, null);
        else
            File.Move(temp, path);
    }
}
