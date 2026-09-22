using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MediaColor = System.Windows.Media.Color;

namespace StickyNoteV2.Views;

public partial class GuideWindow : Window
{
    private readonly GuidePage[] _pages =
    {
        new("Write anything",
            "Click a note and type. Lists, reminders, and ideas all save by themselves.",
            "pack://application:,,,/Assets/Guide/01-write.png"),
        new("Color-code your notes",
            "Right-click the title bar and pick a note color so work, home, and ideas stay distinct.",
            "pack://application:,,,/Assets/Guide/02-color.png"),
        new("Make it your font",
            "Right-click → Font to change family, size, bold, and italic.",
            "pack://application:,,,/Assets/Guide/03-font.png"),
        new("Change the text color",
            "Same menu → Text Color. Great for headings or making a note pop.",
            "pack://application:,,,/Assets/Guide/04-text-color.png"),
        new("Pin on top",
            "Click 📌. Pinned notes stay visible over other windows — even after Win+D.",
            "pack://application:,,,/Assets/Guide/05-pin.png"),
        new("Make another note",
            "Click + on any note, or double-click the tray icon.",
            "pack://application:,,,/Assets/Guide/06-new.png"),
        new("Hide and show everything",
            "Press Alt+` to hide all notes, then press it again to bring them back. Right-click a note for colors and fonts.",
            "pack://application:,,,/Assets/Guide/07-menu.png"),
    };

    private int _index;
    public bool ShowOnStartup { get; private set; } = true;

    public GuideWindow(bool showOnStartup = true)
    {
        InitializeComponent();
        ShowAgainCheck.IsChecked = showOnStartup;
        ShowOnStartup = showOnStartup;
        ShowPage(0);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Finish();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_index > 0)
            ShowPage(_index - 1);
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_index >= _pages.Length - 1)
            Finish();
        else
            ShowPage(_index + 1);
    }

    private void Finish()
    {
        ShowOnStartup = ShowAgainCheck.IsChecked == true;
        Close();
    }

    private void ShowPage(int index)
    {
        _index = index;
        var page = _pages[index];

        TitleText.Text = page.Title;
        SubtitleText.Text = $"{index + 1} of {_pages.Length}";
        BodyText.Text = page.Body;

        try
        {
            ExampleImage.Source = new BitmapImage(new Uri(page.ImageUri));
        }
        catch
        {
            ExampleImage.Source = null;
        }

        BackButton.IsEnabled = index > 0;
        NextButton.Content = index == _pages.Length - 1 ? "Get started" : "Next";

        DotsPanel.Children.Clear();
        for (var i = 0; i < _pages.Length; i++)
        {
            DotsPanel.Children.Add(new Ellipse
            {
                Width = i == index ? 18 : 8,
                Height = 8,
                Margin = new Thickness(3, 0, 3, 0),
                Fill = new SolidColorBrush(i == index
                    ? MediaColor.FromRgb(17, 24, 39)
                    : MediaColor.FromRgb(209, 213, 219))
            });
        }
    }

    private sealed record GuidePage(string Title, string Body, string ImageUri);
}
