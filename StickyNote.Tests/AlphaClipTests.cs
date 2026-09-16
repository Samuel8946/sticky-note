using Xunit;

namespace StickyNote.Tests;

public class AlphaClipTests
{
    private static readonly Size Client = new(200, 100);

    [Fact]
    public void Error_returns_false()
    {
        Assert.False(AlphaClip.TryGetStampRect(0, 0, 0, 200, 100, Client, out _));
    }

    [Fact]
    public void NullRegion_returns_false()
    {
        Assert.False(AlphaClip.TryGetStampRect(Native.NULLREGION, 0, 0, 200, 100, Client, out _));
    }

    [Fact]
    public void SimpleRegion_full_client()
    {
        Assert.True(AlphaClip.TryGetStampRect(Native.SIMPLEREGION, 0, 0, 200, 100, Client, out Rectangle stamp));
        Assert.Equal(new Rectangle(0, 0, 200, 100), stamp);
    }

    [Fact]
    public void SimpleRegion_offscreen_crop()
    {
        Assert.True(AlphaClip.TryGetStampRect(Native.SIMPLEREGION, -40, 10, 180, 90, Client, out Rectangle stamp));
        Assert.Equal(new Rectangle(0, 10, 180, 80), stamp);
    }

    [Fact]
    public void SimpleRegion_clamps_to_client()
    {
        Assert.True(AlphaClip.TryGetStampRect(Native.SIMPLEREGION, -10, -10, 500, 500, Client, out Rectangle stamp));
        Assert.Equal(new Rectangle(0, 0, 200, 100), stamp);
    }

    [Fact]
    public void SimpleRegion_empty_after_clamp_returns_false()
    {
        Assert.False(AlphaClip.TryGetStampRect(Native.SIMPLEREGION, 200, 0, 250, 50, Client, out _));
        Assert.False(AlphaClip.TryGetStampRect(Native.SIMPLEREGION, 0, 100, 50, 150, Client, out _));
        Assert.False(AlphaClip.TryGetStampRect(Native.SIMPLEREGION, 50, 50, 50, 80, Client, out _));
    }

    [Fact]
    public void ComplexRegion_returns_bounding_box()
    {
        // Holes are the caller's problem (GetRandomRgn SYSRGN). The box itself must still stamp.
        Assert.True(AlphaClip.TryGetStampRect(Native.COMPLEXREGION, 10, 20, 80, 70, Client, out Rectangle stamp));
        Assert.Equal(new Rectangle(10, 20, 70, 50), stamp);
    }

    [Fact]
    public void Width_strip_is_on_the_right_edge()
    {
        Assert.True(AlphaClip.TryGetWidthStrip(new Size(120, 100), new Size(200, 100), out Rectangle strip));
        Assert.Equal(new Rectangle(120, 0, 80, 100), strip);
    }

    [Fact]
    public void Height_strip_is_on_the_bottom_edge()
    {
        Assert.True(AlphaClip.TryGetHeightStrip(new Size(200, 60), new Size(200, 100), out Rectangle strip));
        Assert.Equal(new Rectangle(0, 60, 200, 40), strip);
    }

    [Fact]
    public void Strips_cover_full_client_on_first_layout()
    {
        Assert.True(AlphaClip.TryGetWidthStrip(Size.Empty, Client, out Rectangle width));
        Assert.Equal(new Rectangle(0, 0, 200, 100), width);

        Assert.True(AlphaClip.TryGetHeightStrip(Size.Empty, Client, out Rectangle height));
        Assert.Equal(new Rectangle(0, 0, 200, 100), height);
    }

    [Fact]
    public void Shrink_and_same_size_produce_no_strips()
    {
        Assert.False(AlphaClip.TryGetWidthStrip(new Size(300, 100), Client, out _));
        Assert.False(AlphaClip.TryGetHeightStrip(new Size(200, 300), Client, out _));
        Assert.False(AlphaClip.TryGetWidthStrip(Client, Client, out _));
        Assert.False(AlphaClip.TryGetHeightStrip(Client, Client, out _));
    }

    [Fact]
    public void Complex_without_visible_region_is_skipped()
    {
        Assert.True(AlphaClip.ShouldSkipComplexWithoutVisibleRegion(Native.COMPLEXREGION, haveVisibleRegion: false));
        Assert.False(AlphaClip.ShouldSkipComplexWithoutVisibleRegion(Native.COMPLEXREGION, haveVisibleRegion: true));
        Assert.False(AlphaClip.ShouldSkipComplexWithoutVisibleRegion(Native.SIMPLEREGION, haveVisibleRegion: false));
    }

    [Fact]
    public void Visible_region_clip_only_when_mapped()
    {
        Assert.True(AlphaClip.ShouldApplyVisibleRegionClip(haveVisibleRegion: true));
        Assert.False(AlphaClip.ShouldApplyVisibleRegionClip(haveVisibleRegion: false));
    }

    [Fact]
    public void Degenerate_current_size_produces_no_strips()
    {
        Assert.False(AlphaClip.TryGetWidthStrip(Size.Empty, new Size(200, 0), out _));
        Assert.False(AlphaClip.TryGetHeightStrip(Size.Empty, new Size(0, 100), out _));
    }
}
