# Sticky Note

A desktop-pinned sticky note for Windows. It behaves like part of the desktop
rather than a normal application window:

- **Survives Show Desktop (Win+D)** — it's a real child window of Explorer's
  desktop (`SHELLDLL_DefView`), not a top-level window, so Win+D has nothing
  to hide.
- **No taskbar button, not in Alt+Tab** — `ShowInTaskbar = false` plus
  `WS_EX_TOOLWINDOW`.
- **Always visible on the desktop, never over other apps** — it draws above
  the desktop icons but sits in the desktop's own z-layer, so normal
  application windows still cover it.
- **Lives in the tray** — right-click the tray icon for Show/Hide, a global
  focus hotkey (`Win+Shift+N`), lock position, font/colors, and a "Start with
  Windows" toggle. It can be dragged into the hidden tray overflow like any
  other icon.
- **Starts at logon and persists across restarts** — registers itself in
  `HKCU\...\Run` on first run. Note text and window settings are written to
  `%APPDATA%\StickyNote\` (`note.txt`, `settings.json`) via atomic
  temp-file-then-replace writes, so a crash or power loss mid-save can't
  corrupt them.

## Build

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) with the
Windows Desktop workload.

```powershell
dotnet publish -c Release -o publish
```

Run `publish\StickyNote.exe`. On first launch it registers itself to start
at logon; toggle that from the tray menu at any time.

## How the desktop pinning works

Reparenting a window into `Progman` or the wallpaper-layer `WorkerW` (the
usual Rainmeter/Wallpaper-Engine trick) leaves it composited but never
painted — it just shows up as a black rectangle. The only parent that
actually renders here is `SHELLDLL_DefView`, the window that owns the
desktop icon list, and only when the window is switched to `WS_CHILD` before
reparenting.

That introduces a second problem: Explorer's icon view paints with plain GDI,
which leaves the alpha channel at zero on a compositing-aware desktop
surface, so the wallpaper shows through the note's text box. `OpaqueTextBox`
(see `OpaqueTextBox.cs`) fixes this by intercepting `WM_PAINT`, copying out
the freshly drawn pixels, forcing alpha to 255, and blitting them back.

See `DesktopPin.cs` and `NoteForm.cs` for the full implementation, including
the `TaskbarCreated` re-attach handling for when Explorer restarts.

## Project layout

| File | Responsibility |
|---|---|
| `Program.cs` | Entry point, single-instance guard |
| `TrayContext.cs` | Tray icon, context menu, app lifetime |
| `NoteForm.cs` | The note window: chrome, drag/resize, desktop attach |
| `OpaqueTextBox.cs` | Fixes alpha-channel bleed-through inside the desktop |
| `DesktopPin.cs` | Locates the `SHELLDLL_DefView` host window |
| `NoteStore.cs` | Atomic persistence of note text and settings |
| `StartupRegistration.cs` | `HKCU\...\Run` registration |
| `Native.cs` | Win32 P/Invoke declarations |
