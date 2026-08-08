using FsCheck.Xunit;
using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfPropertyTests
{
    [Property(MaxTest = 200)]
    public void Rotating_a_calculated_size_sideways_swaps_dimensions(
        int widthSeed,
        int heightSeed,
        int dpiSeed,
        int scaleSeed)
    {
        var pageWidth = ((uint)widthSeed % 2_000) + 1;
        var pageHeight = ((uint)heightSeed % 2_000) + 1;
        var options = new PdfPageRenderOptions
        {
            Dpi = ((uint)dpiSeed % 600) + 1,
            Scale = (((uint)scaleSeed % 400) + 1) / 100d,
        };

        var normal = options.GetPixelSize(pageWidth, pageHeight);
        options.Rotation = PdfPageRotation.Rotate90;
        var rotated = options.GetPixelSize(pageWidth, pageHeight);

        Assert.Equal(normal.Width, rotated.Height);
        Assert.Equal(normal.Height, rotated.Width);
        Assert.True(normal.Width > 0);
        Assert.True(normal.Height > 0);
    }

    [Property(MaxTest = 200)]
    public void Grayscale_conversion_preserves_alpha_and_equalizes_color_channels(byte[] input)
    {
        var pixelCount = Math.Max(1, Math.Min(input.Length / 4, 1_024));
        var pixels = new byte[pixelCount * 4];
        input.AsSpan(0, Math.Min(input.Length, pixels.Length)).CopyTo(pixels);
        var originalAlpha = Enumerable.Range(0, pixelCount)
            .Select(index => pixels[(index * 4) + 3])
            .ToArray();
        var bitmap = new PdfBitmap(pixelCount, 1, pixelCount * 4, pixels);

        PdfImageConverter.ApplyColorMode(bitmap, PdfImageColorMode.Grayscale);

        for (var index = 0; index < pixelCount; index++)
        {
            var offset = index * 4;
            Assert.Equal(pixels[offset], pixels[offset + 1]);
            Assert.Equal(pixels[offset], pixels[offset + 2]);
            Assert.Equal(originalAlpha[index], pixels[offset + 3]);
        }
    }
}
