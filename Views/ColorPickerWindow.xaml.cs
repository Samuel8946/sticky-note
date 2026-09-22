using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfPoint = System.Windows.Point;

namespace StickyNoteV2.Views;

public partial class ColorPickerWindow : Window
{
    private bool _isDragging;
    public string SelectedColor { get; private set; } = "#FFF59D";
    public bool Confirmed { get; private set; }

    public ColorPickerWindow(string initialColor)
    {
        InitializeComponent();
        SelectedColor = initialColor;
        HexInput.Text = initialColor;
        UpdatePreview();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void UpdatePreview()
    {
        try
        {
            var color = (WpfColor)WpfColorConverter.ConvertFromString(SelectedColor);
            PreviewSwatch.Background = new SolidColorBrush(color);
        }
        catch { }
    }

    private void ColorSpectrum_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        Mouse.Capture(ColorSpectrumBorder);
        UpdateColorFromSpectrum(e.GetPosition(ColorSpectrumBorder));
    }

    private void ColorSpectrum_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging)
        {
            UpdateColorFromSpectrum(e.GetPosition(ColorSpectrumBorder));
        }
    }

    private void ColorSpectrum_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        Mouse.Capture(null);
    }

    private void UpdateColorFromSpectrum(WpfPoint position)
    {
        var width = ColorSpectrumBorder.ActualWidth;
        var height = ColorSpectrumBorder.ActualHeight;

        // Clamp position
        var x = Math.Max(0, Math.Min(width, position.X));
        var y = Math.Max(0, Math.Min(height, position.Y));

        // Update selector position
        Canvas.SetLeft(SpectrumSelector, x - 8);
        Canvas.SetTop(SpectrumSelector, y - 8);

        // Calculate hue from X (0-360)
        var hue = (x / width) * 360;

        // Calculate saturation and value from Y
        // Top = full saturation, bright
        // Middle = full saturation, medium brightness
        // Bottom = dark
        double saturation, value;
        
        if (y < height / 2)
        {
            // Top half: blend from white to pure hue
            saturation = (y / (height / 2));
            value = 1.0;
        }
        else
        {
            // Bottom half: blend from pure hue to black
            saturation = 1.0;
            value = 1.0 - ((y - height / 2) / (height / 2));
        }

        var color = HsvToRgb(hue, saturation, value);
        SelectedColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        HexInput.Text = SelectedColor;
        UpdatePreview();
    }

    private WpfColor HsvToRgb(double h, double s, double v)
    {
        double r, g, b;

        var i = (int)(h / 60) % 6;
        var f = h / 60 - (int)(h / 60);
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);

        switch (i)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }

        return WpfColor.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton button && button.Tag is string color)
        {
            SelectedColor = color;
            HexInput.Text = color;
            UpdatePreview();
        }
    }

    private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = HexInput.Text;
        if (!text.StartsWith("#"))
        {
            text = "#" + text;
        }

        if (text.Length == 7)
        {
            try
            {
                var color = (WpfColor)WpfColorConverter.ConvertFromString(text);
                SelectedColor = text.ToUpper();
                UpdatePreview();
            }
            catch { }
        }
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
