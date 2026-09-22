# StickNote

A modern sticky-notes app for Windows. Notes live on your desktop, save automatically, and stay out of the way until you need them.

![Write a note](Assets/Guide/01-write.png)

## Quick start

1. Download **StickNote-win-x64.zip** from [Releases](https://github.com/Samuel8946/sticky-note/releases).
2. Run `StickNote.exe` (.NET 8 runtime required).
3. On first launch, a visual guide walks through the essentials. You can open it later from the tray: **How to use StickNote**.

Hide or show every note at any time with **Alt+`**.

## How to use

### 1. Write anything

Click a note and type. Lists, reminders, and ideas all save by themselves.

![Grocery list note](Assets/Guide/01-write.png)

### 2. Color-code your notes

Right-click the **title bar** (not the text) and pick a note color so work, home, and ideas stay distinct.

![Blue work note](Assets/Guide/02-color.png)

### 3. Make it your font

Right-click → **Font** to change family, size, bold, and italic.

![Italic idea note](Assets/Guide/03-font.png)

### 4. Change the text color

Same menu → **Text Color**. Useful for headings or making one line stand out.

![Green personal note](Assets/Guide/04-text-color.png)

### 5. Pin on top

Click **📌**. Pinned notes stay visible over other windows — including after Win+D.

![Pinned grocery note](Assets/Guide/05-pin.png)

### 6. Make another note

Click **+** on any note, or double-click the tray icon.

![New blank note](Assets/Guide/06-new.png)

### 7. Hide, show, and style

Press **Alt+`** to hide all notes, then press it again to bring them back.

Right-click the title bar for note color, text color, and fonts:

![Color and font menu](Assets/Guide/07-menu.png)

## Tray menu

- New Note
- Show / Hide All Notes (`Alt+``)
- Start with Windows
- How to use StickNote
- Exit

## Requirements

- Windows 10/11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Data

Notes are saved to `%AppData%\StickyNoteV2\notes.json`.

## Build

```bash
dotnet publish -c Release -o publish-v2
```
