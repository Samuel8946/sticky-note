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
    
    // Font settings
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 14;
    public bool IsBold { get; set; } = false;
    public bool IsItalic { get; set; } = false;
    public string TextColor { get; set; } = "#000000"; // Default black
}

public static class NoteColors
{
    // Preset colors - pastel palette
    public static readonly string Yellow = "#FFF59D";
    public static readonly string Pink = "#F48FB1";
    public static readonly string Blue = "#90CAF9";
    public static readonly string Green = "#A5D6A7";
    public static readonly string Orange = "#FFCC80";
    public static readonly string Purple = "#CE93D8";
    
    // Extended palette
    public static readonly string Red = "#EF9A9A";
    public static readonly string Teal = "#80CBC4";
    public static readonly string Indigo = "#9FA8DA";
    public static readonly string Lime = "#E6EE9C";
    public static readonly string Amber = "#FFE082";
    public static readonly string Cyan = "#80DEEA";
    public static readonly string Brown = "#BCAAA4";
    public static readonly string Grey = "#EEEEEE";
    public static readonly string White = "#FFFFFF";

    public static readonly string[] All = { 
        Yellow, Pink, Blue, Green, Orange, Purple,
        Red, Teal, Indigo, Lime, Amber, Cyan, Brown, Grey, White
    };
}

public static class NoteFonts
{
    public static readonly string[] Families = {
        "Segoe UI",
        "Arial",
        "Calibri",
        "Consolas",
        "Comic Sans MS",
        "Courier New",
        "Georgia",
        "Impact",
        "Lucida Console",
        "Tahoma",
        "Times New Roman",
        "Trebuchet MS",
        "Verdana"
    };

    public static readonly double[] Sizes = { 10, 12, 14, 16, 18, 20, 24, 28, 32, 36 };
}
