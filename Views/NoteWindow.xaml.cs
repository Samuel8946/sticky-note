using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StickyNoteV2.Models;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

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

        // Set pin state
        Topmost = note.IsPinned;
        UpdatePinIcon();

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

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string color)
        {
            SetColor(color);
            NoteChanged?.Invoke(_note);
        }
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
