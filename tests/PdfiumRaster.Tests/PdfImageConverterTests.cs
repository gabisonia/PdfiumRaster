using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfImageConverterTests
{
    [Fact]
    public void RenderPageNumber_rejects_zero_page_number()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PdfImageConverter.RenderPageNumber("sample.pdf", 0));
    }

    [Fact]
    public void ApplyColorMode_grayscale_sets_rgb_channels_to_luminance()
    {
        var bitmap = new PdfBitmap(
            width: 1,
            height: 1,
            stride: 4,
            pixels: [10, 20, 30, 255]);

        PdfImageConverter.ApplyColorMode(bitmap, PdfImageColorMode.Grayscale);

        Assert.Equal(22, bitmap.Pixels[0]);
        Assert.Equal(22, bitmap.Pixels[1]);
        Assert.Equal(22, bitmap.Pixels[2]);
        Assert.Equal(255, bitmap.Pixels[3]);
    }

    [Fact]
    public void ApplyColorMode_black_and_white_thresholds_luminance()
    {
        var bitmap = new PdfBitmap(
            width: 2,
            height: 1,
            stride: 8,
            pixels:
            [
                10, 10, 10, 255,
                240, 240, 240, 255,
            ]);

        PdfImageConverter.ApplyColorMode(bitmap, PdfImageColorMode.BlackAndWhite, blackAndWhiteThreshold: 128);

        Assert.Equal([0, 0, 0, 255, 255, 255, 255, 255], bitmap.Pixels);
    }
}
