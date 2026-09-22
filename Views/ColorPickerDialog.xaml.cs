using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using StickyNoteV2.Models;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace StickyNoteV2.Views;

public partial class ColorPickerDialog : Window
{
    public string SelectedColor { get; private set; } = "#FFF59D";

    public ColorPickerDialog(string? initialColor = null)
    {
        InitializeComponent();

        // Load preset colors
        PresetColors.ItemsSource = NoteColors.All;

        // Set initial color
        if (!string.IsNullOrEmpty(initialColor))
        {
            SelectedColor = initialColor;
            SetColorFromHex(initialColor);
        }

        UpdatePreview();
    }

    private void SetColorFromHex(string hex)
    {
        try
        {
            var color = (WpfColor)WpfColorConverter.ConvertFromString(hex);
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;
        }
        catch { }
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (RedSlider == null || GreenSlider == null || BlueSlider == null) return;

        var r = (byte)RedSlider.Value;
        var g = (byte)GreenSlider.Value;
        var b = (byte)BlueSlider.Value;

        var color = WpfColor.FromRgb(r, g, b);
        SelectedColor = $"#{r:X2}{g:X2}{b:X2}";

        ColorPreview.Background = new SolidColorBrush(color);
        HexValue.Text = SelectedColor;
    }

    private void PresetColor_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Border border && border.Background is SolidColorBrush brush)
        {
            var color = brush.Color;
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
