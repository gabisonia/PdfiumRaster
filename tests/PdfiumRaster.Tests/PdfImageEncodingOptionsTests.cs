using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfImageEncodingOptionsTests
{
    [Fact]
    public void Defaults_preserve_quality_and_skia_png_compression()
    {
        var options = new PdfImageEncodingOptions();

        Assert.Equal(100, options.Quality);
        Assert.Null(options.PngCompressionLevel);
    }

    [Fact]
    public void Fast_returns_independent_options()
    {
        var first = PdfImageEncodingOptions.Fast;
        var second = PdfImageEncodingOptions.Fast;

        Assert.NotSame(first, second);
        Assert.Equal(85, first.Quality);
        Assert.Equal(1, first.PngCompressionLevel);
    }

    [Fact]
    public void Quality_rejects_values_outside_zero_to_one_hundred()
    {
        var options = new PdfImageEncodingOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Quality = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Quality = 101);
        options.Quality = 0;
        options.Quality = 100;
    }

    [Fact]
    public void Png_compression_rejects_values_outside_zero_to_nine()
    {
        var options = new PdfImageEncodingOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.PngCompressionLevel = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.PngCompressionLevel = 10);
        options.PngCompressionLevel = null;
        options.PngCompressionLevel = 0;
        options.PngCompressionLevel = 9;
    }
}
