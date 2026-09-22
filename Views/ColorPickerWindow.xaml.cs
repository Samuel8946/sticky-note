using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfButton = System.Windows.Controls.Button;
using WpfPoint = System.Windows.Point;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace StickyNoteV2.Views;

public partial class ColorPickerWindow : Window
{
    private bool _isDraggingSatBright;
    private bool _isDraggingHue;
    private WpfColor _initialColor;
    private WpfColor _selectedColor;
    
    private double _hue = 0;
    private double _saturation = 1;
    private double _brightness = 1;

    public WpfColor SelectedColor => _selectedColor;
    public new bool DialogResult { get; private set; }
    public event Action<WpfColor>? ColorApplied;

    public ColorPickerWindow(WpfColor initialColor)
    {
        InitializeComponent();
        _initialColor = initialColor;
        _selectedColor = initialColor;
        
        // Convert initial color to HSB
        RgbToHsb(initialColor, out _hue, out _saturation, out _brightness);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Set initial colors
        CurrentColorSwatch.Background = new SolidColorBrush(_initialColor);
        UpdateUI();
        UpdateSelectors();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    #region Saturation/Brightness Box

    private void SatBright_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSatBright = true;
        Mouse.Capture(SatBrightBox);
        UpdateSatBrightFromPosition(e.GetPosition(SatBrightBox));
    }

    private void SatBright_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_isDraggingSatBright)
        {
            UpdateSatBrightFromPosition(e.GetPosition(SatBrightBox));
        }
    }

    private void SatBright_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSatBright = false;
        Mouse.Capture(null);
    }

    private void UpdateSatBrightFromPosition(WpfPoint position)
    {
        var width = SatBrightBox.ActualWidth;
        var height = SatBrightBox.ActualHeight;
        if (width <= 0 || height <= 0) return;

        var x = Math.Max(0, Math.Min(width, position.X));
        var y = Math.Max(0, Math.Min(height, position.Y));

        _saturation = x / width;
        _brightness = 1 - (y / height);

        UpdateColorFromHsb();
        UpdateUI();
        UpdateSelectors();
    }

    #endregion

    #region Hue Slider

    private void Hue_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingHue = true;
        Mouse.Capture(HueSlider);
        UpdateHueFromPosition(e.GetPosition(HueSlider));
    }

    private void Hue_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_isDraggingHue)
        {
            UpdateHueFromPosition(e.GetPosition(HueSlider));
        }
    }

    private void Hue_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingHue = false;
        Mouse.Capture(null);
    }

    private void UpdateHueFromPosition(WpfPoint position)
    {
        var width = HueSlider.ActualWidth;
        if (width <= 0) return;

        var x = Math.Max(0, Math.Min(width, position.X));
        _hue = (x / width) * 360;

        UpdateColorFromHsb();
        UpdateUI();
        UpdateSelectors();
    }

    #endregion

    private void UpdateColorFromHsb()
    {
        _selectedColor = HsbToRgb(_hue, _saturation, _brightness);
    }

    private void UpdateUI()
    {
        // Update the saturation/brightness box background to show current hue
        var hueColor = HsbToRgb(_hue, 1, 1);
        SatBrightBackground.Color = hueColor;

        // Update preview
        NewColorSwatch.Background = new SolidColorBrush(_selectedColor);

        // Update hex (without triggering TextChanged)
        HexInput.TextChanged -= HexInput_TextChanged;
        HexInput.Text = $"#{_selectedColor.R:X2}{_selectedColor.G:X2}{_selectedColor.B:X2}";
        HexInput.TextChanged += HexInput_TextChanged;
    }

    private void UpdateSelectors()
    {
        // Position hue selector
        var hueWidth = HueSlider.ActualWidth;
        if (hueWidth > 0)
        {
            var hueX = (_hue / 360) * hueWidth;
            Canvas.SetLeft(HueSelector, hueX - 4);
        }

        // Position sat/bright selector
        var sbWidth = SatBrightBox.ActualWidth;
        var sbHeight = SatBrightBox.ActualHeight;
        if (sbWidth > 0 && sbHeight > 0)
        {
            var sbX = _saturation * sbWidth;
            var sbY = (1 - _brightness) * sbHeight;
            Canvas.SetLeft(SatBrightSelector, sbX - 10);
            Canvas.SetTop(SatBrightSelector, sbY - 10);
        }
    }

    private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = HexInput.Text.Trim();
        if (!text.StartsWith("#")) text = "#" + text;

        if (text.Length == 7)
        {
            try
            {
                var color = (WpfColor)WpfColorConverter.ConvertFromString(text);
                _selectedColor = color;
                RgbToHsb(color, out _hue, out _saturation, out _brightness);
                
                // Update UI without changing hex input
                var hueColor = HsbToRgb(_hue, 1, 1);
                SatBrightBackground.Color = hueColor;
                NewColorSwatch.Background = new SolidColorBrush(_selectedColor);
                UpdateSelectors();
            }
            catch { }
        }
    }

    private void QuickColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton button && button.Tag is string colorStr)
        {
            try
            {
                _selectedColor = (WpfColor)WpfColorConverter.ConvertFromString(colorStr);
                RgbToHsb(_selectedColor, out _hue, out _saturation, out _brightness);
                UpdateUI();
                UpdateSelectors();
            }
            catch { }
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        ColorApplied?.Invoke(_selectedColor);
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

    #region Color Conversion

    private static WpfColor HsbToRgb(double h, double s, double b)
    {
        double r, g, bl;
        
        if (s == 0)
        {
            r = g = bl = b;
        }
        else
        {
            var sector = h / 60;
            var i = (int)Math.Floor(sector);
            var f = sector - i;
            var p = b * (1 - s);
            var q = b * (1 - s * f);
            var t = b * (1 - s * (1 - f));

            switch (i % 6)
            {
                case 0: r = b; g = t; bl = p; break;
                case 1: r = q; g = b; bl = p; break;
                case 2: r = p; g = b; bl = t; break;
                case 3: r = p; g = q; bl = b; break;
                case 4: r = t; g = p; bl = b; break;
                default: r = b; g = p; bl = q; break;
            }
        }

        return WpfColor.FromRgb(
            (byte)Math.Round(r * 255),
            (byte)Math.Round(g * 255),
            (byte)Math.Round(bl * 255));
    }

    private static void RgbToHsb(WpfColor color, out double h, out double s, out double b)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double bl = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, bl));
        double min = Math.Min(r, Math.Min(g, bl));
        double delta = max - min;

        // Brightness
        b = max;

        // Saturation
        s = max == 0 ? 0 : delta / max;

        // Hue
        if (delta == 0)
        {
            h = 0;
        }
        else if (max == r)
        {
            h = 60 * (((g - bl) / delta) % 6);
        }
        else if (max == g)
        {
            h = 60 * (((bl - r) / delta) + 2);
        }
        else
        {
            h = 60 * (((r - g) / delta) + 4);
        }

        if (h < 0) h += 360;
    }

    #endregion
}
