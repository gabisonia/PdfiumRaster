using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfImageConverterTests
{
    [Fact]
    public void SavePng_writes_png_to_stream()
    {
        using var stream = new MemoryStream();

        PdfImageConverter.SavePng(
            GetTestPdfPath("TestAssets/smoke.pdf"),
            pageNumber: 1,
            stream,
            new PdfImageConversionOptions
            {
                Render = new PdfPageRenderOptions { Dpi = 96 },
            });

        var bytes = stream.ToArray();

        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    [Fact]
    public void SaveJpeg_writes_jpeg_to_stream()
    {
        using var stream = new MemoryStream();

        PdfImageConverter.SaveJpeg(
            GetTestPdfPath("TestAssets/smoke.pdf"),
            pageNumber: 1,
            stream,
            new PdfImageConversionOptions
            {
                Render = new PdfPageRenderOptions { Dpi = 96 },
            });

        var bytes = stream.ToArray();

        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }

    [Fact]
    public void SaveWebp_writes_webp_to_stream()
    {
        using var stream = new MemoryStream();

        PdfImageConverter.SaveWebp(
            GetTestPdfPath("TestAssets/smoke.pdf"),
            pageNumber: 1,
            stream,
            new PdfImageConversionOptions
            {
                Render = new PdfPageRenderOptions { Dpi = 96 },
            });

        var bytes = stream.ToArray();

        Assert.Equal((byte)'R', bytes[0]);
        Assert.Equal((byte)'I', bytes[1]);
        Assert.Equal((byte)'F', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
        Assert.Equal((byte)'W', bytes[8]);
        Assert.Equal((byte)'E', bytes[9]);
        Assert.Equal((byte)'B', bytes[10]);
        Assert.Equal((byte)'P', bytes[11]);
    }

    [Fact]
    public void SavePage_stream_rejects_null_stream()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PdfImageConverter.SavePage(GetTestPdfPath("TestAssets/smoke.pdf"), 0, (Stream)null!));
    }

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

    private static string GetTestPdfPath(string relativePdfPath)
    {
        return Path.Combine(AppContext.BaseDirectory, relativePdfPath);
    }
}
