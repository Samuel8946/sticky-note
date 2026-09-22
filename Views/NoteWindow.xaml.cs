using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StickyNoteV2.Models;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfFontFamily = System.Windows.Media.FontFamily;

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

        // Set content and colors
        NoteTextBox.Text = note.Content;
        SetNoteColor(note.Color);
        SetTextColor(note.TextColor);

        // Set font settings
        SetFontFamily(note.FontFamily);
        SetFontSize(note.FontSize);
        SetBold(note.IsBold);
        SetItalic(note.IsItalic);

        // Set pin state
        Topmost = note.IsPinned;
        UpdatePinIcon();

        // Build dynamic menus
        BuildFontFamilyMenu();
        BuildFontSizeMenu();

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

    #region Color Methods

    private void SetNoteColor(string hexColor)
    {
        try
        {
            var color = (WpfColor)WpfColorConverter.ConvertFromString(hexColor);
            MainBorder.Background = new SolidColorBrush(color);
            _note.Color = hexColor;
        }
        catch
        {
            MainBorder.Background = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#FFF59D"));
            _note.Color = "#FFF59D";
        }
    }

    private void SetTextColor(string hexColor)
    {
        try
        {
            var color = (WpfColor)WpfColorConverter.ConvertFromString(hexColor);
            NoteTextBox.Foreground = new SolidColorBrush(color);
            _note.TextColor = hexColor;
        }
        catch
        {
            NoteTextBox.Foreground = new SolidColorBrush(Colors.Black);
            _note.TextColor = "#000000";
        }
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string color)
        {
            SetNoteColor(color);
            NoteChanged?.Invoke(_note);
        }
    }

    private void TextColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string color)
        {
            SetTextColor(color);
            NoteChanged?.Invoke(_note);
        }
    }

    private void CustomColor_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerDialog(_note.Color) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            SetNoteColor(dialog.SelectedColor);
            NoteChanged?.Invoke(_note);
        }
    }

    private void CustomTextColor_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerDialog(_note.TextColor) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            SetTextColor(dialog.SelectedColor);
            NoteChanged?.Invoke(_note);
        }
    }

    #endregion

    #region Font Methods

    private void BuildFontFamilyMenu()
    {
        FontFamilyMenu.Items.Clear();
        foreach (var font in NoteFonts.Families)
        {
            var item = new MenuItem
            {
                Header = font,
                Tag = font,
                FontFamily = new WpfFontFamily(font),
                IsCheckable = true,
                IsChecked = font == _note.FontFamily
            };
            item.Click += FontFamily_Click;
            FontFamilyMenu.Items.Add(item);
        }
    }

    private void BuildFontSizeMenu()
    {
        FontSizeMenu.Items.Clear();
        foreach (var size in NoteFonts.Sizes)
        {
            var item = new MenuItem
            {
                Header = size.ToString(),
                Tag = size,
                IsCheckable = true,
                IsChecked = Math.Abs(size - _note.FontSize) < 0.1
            };
            item.Click += FontSize_Click;
            FontSizeMenu.Items.Add(item);
        }
    }

    private void SetFontFamily(string fontFamily)
    {
        try
        {
            NoteTextBox.FontFamily = new WpfFontFamily(fontFamily);
            _note.FontFamily = fontFamily;
        }
        catch
        {
            NoteTextBox.FontFamily = new WpfFontFamily("Segoe UI");
            _note.FontFamily = "Segoe UI";
        }
    }

    private void SetFontSize(double size)
    {
        NoteTextBox.FontSize = size;
        _note.FontSize = size;
    }

    private void SetBold(bool isBold)
    {
        NoteTextBox.FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal;
        _note.IsBold = isBold;
        BoldMenuItem.IsChecked = isBold;
    }

    private void SetItalic(bool isItalic)
    {
        NoteTextBox.FontStyle = isItalic ? FontStyles.Italic : FontStyles.Normal;
        _note.IsItalic = isItalic;
        ItalicMenuItem.IsChecked = isItalic;
    }

    private void FontFamily_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string fontFamily)
        {
            SetFontFamily(fontFamily);
            
            // Update checkmarks
            foreach (MenuItem item in FontFamilyMenu.Items)
            {
                item.IsChecked = item.Tag as string == fontFamily;
            }
            
            NoteChanged?.Invoke(_note);
        }
    }

    private void FontSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is double size)
        {
            SetFontSize(size);
            
            // Update checkmarks
            foreach (MenuItem item in FontSizeMenu.Items)
            {
                item.IsChecked = item.Tag is double s && Math.Abs(s - size) < 0.1;
            }
            
            NoteChanged?.Invoke(_note);
        }
    }

    private void Bold_Click(object sender, RoutedEventArgs e)
    {
        SetBold(BoldMenuItem.IsChecked);
        NoteChanged?.Invoke(_note);
    }

    private void Italic_Click(object sender, RoutedEventArgs e)
    {
        SetItalic(ItalicMenuItem.IsChecked);
        NoteChanged?.Invoke(_note);
    }

    #endregion

    #region Window Methods

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

    public void EnsureOnScreen()
    {
        var screen = SystemParameters.WorkArea;
        
        if (Left < 0) Left = 20;
        if (Top < 0) Top = 20;
        if (Left + Width > screen.Width) Left = screen.Width - Width - 20;
        if (Top + Height > screen.Height) Top = screen.Height - Height - 20;
    }

    #endregion
}
