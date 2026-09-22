namespace StickyNoteV2.Models;

public class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFF59D"; // Default yellow
    public double X { get; set; } = 100;
    public double Y { get; set; } = 100;
    public double Width { get; set; } = 250;
    public double Height { get; set; } = 250;
    public bool IsPinned { get; set; } = false;
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime Modified { get; set; } = DateTime.Now;
}

public static class NoteColors
{
    public static readonly string Yellow = "#FFF59D";
    public static readonly string Pink = "#F48FB1";
    public static readonly string Blue = "#90CAF9";
    public static readonly string Green = "#A5D6A7";
    public static readonly string Orange = "#FFCC80";
    public static readonly string Purple = "#CE93D8";

    public static readonly string[] All = { Yellow, Pink, Blue, Green, Orange, Purple };
}
