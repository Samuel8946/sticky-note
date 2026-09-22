using System.Windows;
using System.Windows.Media;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace StickyNoteV2.Views;

public partial class FontPickerWindow : Window
{
    private static readonly double[] FontSizes = { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 32, 36, 48, 72 };

    public string SelectedFontFamily { get; private set; }
    public double SelectedFontSize { get; private set; }
    public bool SelectedBold { get; private set; }
    public bool SelectedItalic { get; private set; }
    public new bool DialogResult { get; private set; }

    public event Action<string, double, bool, bool>? FontApplied;

    public FontPickerWindow(string fontFamily, double fontSize, bool bold, bool italic)
    {
        InitializeComponent();

        SelectedFontFamily = fontFamily;
        SelectedFontSize = fontSize;
        SelectedBold = bold;
        SelectedItalic = italic;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Populate font list - filter out symbol fonts that can't display text
        var fonts = Fonts.SystemFontFamilies
            .Where(f => HasLatinCharacters(f))
            .OrderBy(f => f.Source)
            .Select(f => f.Source)
            .ToList();

        FontList.ItemsSource = fonts;

        // Populate size list
        SizeList.ItemsSource = FontSizes;

        // Select current font
        var fontIndex = fonts.IndexOf(SelectedFontFamily);
        if (fontIndex >= 0)
        {
            FontList.SelectedIndex = fontIndex;
            FontList.ScrollIntoView(FontList.SelectedItem);
        }

        // Select current size
        var sizeIndex = Array.IndexOf(FontSizes, SelectedFontSize);
        if (sizeIndex >= 0)
        {
            SizeList.SelectedIndex = sizeIndex;
        }
        else
        {
            // Find closest size
            var closest = FontSizes.OrderBy(s => Math.Abs(s - SelectedFontSize)).First();
            SizeList.SelectedItem = closest;
        }

        // Set style checkboxes
        BoldCheckBox.IsChecked = SelectedBold;
        ItalicCheckBox.IsChecked = SelectedItalic;

        UpdatePreview();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void FontList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (FontList.SelectedItem is string fontName)
        {
            SelectedFontFamily = fontName;
            UpdatePreview();
        }
    }

    private void SizeList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SizeList.SelectedItem is double size)
        {
            SelectedFontSize = size;
            UpdatePreview();
        }
    }

    private void Style_Changed(object sender, RoutedEventArgs e)
    {
        SelectedBold = BoldCheckBox.IsChecked ?? false;
        SelectedItalic = ItalicCheckBox.IsChecked ?? false;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (PreviewText == null) return;

        try
        {
            PreviewText.FontFamily = new WpfFontFamily(SelectedFontFamily);
        }
        catch
        {
            PreviewText.FontFamily = new WpfFontFamily("Segoe UI");
        }

        PreviewText.FontSize = Math.Min(SelectedFontSize, 24); // Cap preview size
        PreviewText.FontWeight = SelectedBold ? FontWeights.Bold : FontWeights.Normal;
        PreviewText.FontStyle = SelectedItalic ? FontStyles.Italic : FontStyles.Normal;

        // Use appropriate sample text
        var sampleText = GetSampleText(SelectedFontFamily);
        PreviewText.Text = sampleText;
    }

    private string GetSampleText(string fontFamily)
    {
        // Check if it's a symbol/icon font
        try
        {
            var font = new WpfFontFamily(fontFamily);
            var typeface = new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            if (typeface.TryGetGlyphTypeface(out var glyphTypeface))
            {
                // Check if standard Latin characters are available
                bool hasLatinChars = glyphTypeface.CharacterToGlyphMap.ContainsKey('A') &&
                                     glyphTypeface.CharacterToGlyphMap.ContainsKey('a');

                if (!hasLatinChars)
                {
                    // Symbol font - show some symbols
                    return "★ ♠ ♣ ♥ ♦ ✓ ✗ ➔ ● ■";
                }
            }
        }
        catch { }

        return "The quick brown fox jumps over the lazy dog";
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        FontApplied?.Invoke(SelectedFontFamily, SelectedFontSize, SelectedBold, SelectedItalic);
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

    private static bool HasLatinCharacters(WpfFontFamily fontFamily)
    {
        try
        {
            var typeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            if (typeface.TryGetGlyphTypeface(out var glyphTypeface))
            {
                // Check if basic Latin letters are available
                return glyphTypeface.CharacterToGlyphMap.ContainsKey('A') &&
                       glyphTypeface.CharacterToGlyphMap.ContainsKey('a') &&
                       glyphTypeface.CharacterToGlyphMap.ContainsKey('b');
            }
        }
        catch { }
        
        return true; // Include if we can't determine
    }
}
