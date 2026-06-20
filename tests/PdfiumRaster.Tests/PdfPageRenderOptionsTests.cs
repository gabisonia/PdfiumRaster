using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfPageRenderOptionsTests
{
    [Fact]
    public void GetPixelSize_converts_points_to_pixels_using_dpi()
    {
        var options = new PdfPageRenderOptions
        {
            Dpi = 144,
        };

        var size = options.GetPixelSize(612, 792);

        Assert.Equal(1224, size.Width);
        Assert.Equal(1584, size.Height);
    }

    [Fact]
    public void GetPixelSize_applies_scale()
    {
        var options = new PdfPageRenderOptions
        {
            Dpi = 72,
            Scale = 2,
        };

        var size = options.GetPixelSize(100, 200);

        Assert.Equal(200, size.Width);
        Assert.Equal(400, size.Height);
    }

    [Fact]
    public void GetPixelSize_swaps_dimensions_when_rotated_sideways()
    {
        var options = new PdfPageRenderOptions
        {
            Dpi = 72,
            Rotation = PdfPageRotation.Rotate90,
        };

        var size = options.GetPixelSize(100, 200);

        Assert.Equal(200, size.Width);
        Assert.Equal(100, size.Height);
    }

    [Fact]
    public void Dpi_rejects_invalid_values()
    {
        var options = new PdfPageRenderOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Dpi = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Dpi = double.NaN);
    }

    [Fact]
    public void Rotation_rejects_invalid_values()
    {
        var options = new PdfPageRenderOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Rotation = (PdfPageRotation)42);
    }
}