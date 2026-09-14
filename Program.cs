namespace StickyNote;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // The logon Run entry and a manual launch can race; keep exactly one note alive.
        using var single = new Mutex(initiallyOwned: true, @"Local\StickyNote.SingleInstance", out bool isFirst);
        if (!isFirst)
            return;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayContext());
    }
}
