using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StickyNoteV2.Models;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfButton = System.Windows.Controls.Button;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WinForms = System.Windows.Forms;

namespace StickyNoteV2.Views;

public partial class NoteWindow : Window
{
    private readonly Note _note;
    
    public event Action<Note>? NoteChanged;
    public event Action<Note>? NoteDeleted;
    public event Action? NewNoteRequested;

    public Note Note => _note;

    public NoteWindow(Note note)
    {
        InitializeComponent();
        _note = note;

        // Set initial position and size
        Left = note.X;
        Top = note.Y;
        Width = note.Width;
        Height = note.Height;

        // Set content and color
        NoteTextBox.Text = note.Content;
        SetColor(note.Color);

        // Set font
        ApplyFont();

        // Set pin state
        Topmost = note.IsPinned;
        UpdatePinIcon();

        // Update menu checkboxes
        BoldMenuItem.IsChecked = note.IsBold;
        ItalicMenuItem.IsChecked = note.IsItalic;

        // Track position/size changes
        LocationChanged += (s, e) => SavePositionSize();
        SizeChanged += (s, e) => SavePositionSize();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Fade in animation
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
        BeginAnimation(OpacityProperty, fadeIn);
    }

    private void ApplyFont()
    {
        try
        {
            NoteTextBox.FontFamily = new WpfFontFamily(_note.FontFamily);
        }
        catch
        {
            NoteTextBox.FontFamily = new WpfFontFamily("Segoe UI");
        }
        
        NoteTextBox.FontSize = _note.FontSize;
        NoteTextBox.FontWeight = _note.IsBold ? FontWeights.Bold : FontWeights.Normal;
        NoteTextBox.FontStyle = _note.IsItalic ? FontStyles.Italic : FontStyles.Normal;
    }

    private void SetColor(string hexColor)
    {
        try
        {
            var color = (WpfColor)WpfColorConverter.ConvertFromString(hexColor);
            MainBorder.Background = new SolidColorBrush(color);
            _note.Color = hexColor;
        }
        catch
        {
            // Fallback to yellow
            MainBorder.Background = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#FFF59D"));
            _note.Color = "#FFF59D";
        }
    }

    private void UpdatePinIcon()
    {
        PinIcon.Opacity = _note.IsPinned ? 1.0 : 0.5;
    }

    private void SavePositionSize()
    {
        _note.X = Left;
        _note.Y = Top;
        _note.Width = Width;
        _note.Height = Height;
        _note.Modified = DateTime.Now;
        NoteChanged?.Invoke(_note);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click to toggle pin
            TogglePin();
        }
        else
        {
            DragMove();
        }
    }

    private void TogglePin()
    {
        _note.IsPinned = !_note.IsPinned;
        Topmost = _note.IsPinned;
        UpdatePinIcon();
        NoteChanged?.Invoke(_note);
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        TogglePin();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        // Confirm delete if note has content
        if (!string.IsNullOrWhiteSpace(_note.Content))
        {
            var result = System.Windows.MessageBox.Show(
                "Delete this note?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        NoteDeleted?.Invoke(_note);
        Close();
    }

    private void NewNote_Click(object sender, RoutedEventArgs e)
    {
        NewNoteRequested?.Invoke();
    }

    private void NoteTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _note.Content = NoteTextBox.Text;
        _note.Modified = DateTime.Now;
        NoteChanged?.Invoke(_note);
    }

    // Preset color buttons
    private void PresetColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton button && button.Tag is string color)
        {
            SetColor(color);
            NoteChanged?.Invoke(_note);
        }
    }

    // Custom color picker using Windows Forms ColorDialog
    private void CustomColor_Click(object sender, RoutedEventArgs e)
    {
        using var colorDialog = new WinForms.ColorDialog
        {
            AllowFullOpen = true,
            AnyColor = true,
            FullOpen = true
        };

        // Set current color
        try
        {
            var currentColor = (WpfColor)WpfColorConverter.ConvertFromString(_note.Color);
            colorDialog.Color = System.Drawing.Color.FromArgb(currentColor.A, currentColor.R, currentColor.G, currentColor.B);
        }
        catch { }

        if (colorDialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            var selectedColor = colorDialog.Color;
            var hexColor = $"#{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";
            SetColor(hexColor);
            NoteChanged?.Invoke(_note);
        }
    }

    // Font picker using Windows Forms FontDialog
    private void ChangeFont_Click(object sender, RoutedEventArgs e)
    {
        using var fontDialog = new WinForms.FontDialog
        {
            AllowVerticalFonts = false,
            AllowScriptChange = true,
            ShowEffects = false
        };

        // Set current font
        try
        {
            var style = System.Drawing.FontStyle.Regular;
            if (_note.IsBold) style |= System.Drawing.FontStyle.Bold;
            if (_note.IsItalic) style |= System.Drawing.FontStyle.Italic;
            
            fontDialog.Font = new System.Drawing.Font(_note.FontFamily, (float)_note.FontSize, style);
        }
        catch { }

        if (fontDialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            var selectedFont = fontDialog.Font;
            _note.FontFamily = selectedFont.FontFamily.Name;
            _note.FontSize = selectedFont.Size;
            _note.IsBold = selectedFont.Bold;
            _note.IsItalic = selectedFont.Italic;
            
            ApplyFont();
            
            // Update menu checkboxes
            BoldMenuItem.IsChecked = _note.IsBold;
            ItalicMenuItem.IsChecked = _note.IsItalic;
            
            NoteChanged?.Invoke(_note);
        }
    }

    // Font size presets
    private void FontSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfMenuItem menuItem && menuItem.Tag is string sizeStr && double.TryParse(sizeStr, out double size))
        {
            _note.FontSize = size;
            ApplyFont();
            NoteChanged?.Invoke(_note);
        }
    }

    // Bold toggle
    private void Bold_Click(object sender, RoutedEventArgs e)
    {
        _note.IsBold = BoldMenuItem.IsChecked;
        ApplyFont();
        NoteChanged?.Invoke(_note);
    }

    // Italic toggle
    private void Italic_Click(object sender, RoutedEventArgs e)
    {
        _note.IsItalic = ItalicMenuItem.IsChecked;
        ApplyFont();
        NoteChanged?.Invoke(_note);
    }

    /// <summary>
    /// Ensures the window is within screen bounds
    /// </summary>
    public void EnsureOnScreen()
    {
        var screen = SystemParameters.WorkArea;
        
        if (Left < 0) Left = 20;
        if (Top < 0) Top = 20;
        if (Left + Width > screen.Width) Left = screen.Width - Width - 20;
        if (Top + Height > screen.Height) Top = screen.Height - Height - 20;
    }
}
