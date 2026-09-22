# Sticky Note v2

A modern, lightweight sticky notes application for Windows built with WPF.

## Features

- **Flat Design** - Clean, minimal UI with pastel color palette
- **Multiple Colors** - Yellow, Pink, Blue, Green, Orange, Purple
- **Always on Top** - Pin notes to stay visible over other windows
- **Resizable** - Drag corners to resize notes
- **System Tray** - Runs quietly in the background
- **Auto-save** - Notes are automatically saved
- **Start with Windows** - Optional auto-start on login

## Requirements

- Windows 10/11
- .NET 8.0 Runtime

## Building

```bash
dotnet build
```

## Running

```bash
dotnet run
```

## Usage

- **Create Note**: Double-click tray icon or click "+" button on any note
- **Change Color**: Right-click note → Colors
- **Pin/Unpin**: Click pin icon or double-click title bar
- **Delete Note**: Click "X" button
- **Move Note**: Drag the title bar
- **Resize Note**: Drag the resize grip (bottom-right corner)

## Keyboard Shortcuts

- `Ctrl+A` - Select all text in a note
- `Tab` - Insert tab character

## Data Storage

Notes are saved to: `%AppData%\StickyNoteV2\notes.json`
