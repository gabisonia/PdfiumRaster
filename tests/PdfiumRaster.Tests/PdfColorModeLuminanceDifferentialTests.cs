using PdfiumRaster;

namespace PdfiumRaster.Tests;

/// <summary>
/// Compares the span-based grayscale and black-and-white luminance passes byte for byte against
/// verbatim copies of the scalar loops they replaced, across widths, packed and padded strides,
/// and threshold edges. The full buffer is compared, including padding bytes.
/// </summary>
public sealed class PdfColorModeLuminanceDifferentialTests
{
    private const int RandomSeed = 246810;

    [Fact]
    public void Grayscale_matches_reference_scalar_loop()
    {
        var random = new Random(RandomSeed);

        for (var width = 1; width <= 40; width++)
        {
            foreach (var paddingBytes in new[] { 0, 1, 3, 4, 12 })
            {
                const int height = 5;
                var stride = width * 4 + paddingBytes;
                var pixels = new byte[stride * height];
                random.NextBytes(pixels);

                var expected = (byte[])pixels.Clone();
                ReferenceApplyGrayscale(expected, width, height, stride);

                var bitmap = new PdfBitmap(width, height, stride, pixels);
                PdfImageConverter.ApplyColorMode(bitmap, PdfImageColorMode.Grayscale);

                Assert.True(
                    expected.AsSpan().SequenceEqual(bitmap.Pixels),
                    $"Grayscale output diverged from the reference loop for width {width}, " +
                    $"padding {paddingBytes}.");
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(254)]
    [InlineData(255)]
    public void Black_and_white_matches_reference_scalar_loop(byte threshold)
    {
        var random = new Random(RandomSeed);

        for (var width = 1; width <= 40; width++)
        {
            foreach (var paddingBytes in new[] { 0, 1, 3, 4, 12 })
            {
                const int height = 5;
                var stride = width * 4 + paddingBytes;
                var pixels = new byte[stride * height];
                random.NextBytes(pixels);

                var expected = (byte[])pixels.Clone();
                ReferenceApplyBlackAndWhite(expected, width, height, stride, threshold);

                var bitmap = new PdfBitmap(width, height, stride, pixels);
                PdfImageConverter.ApplyColorMode(bitmap, PdfImageColorMode.BlackAndWhite, threshold);

                Assert.True(
                    expected.AsSpan().SequenceEqual(bitmap.Pixels),
                    $"Black-and-white output diverged from the reference loop for width {width}, " +
                    $"padding {paddingBytes}, threshold {threshold}.");
            }
        }
    }

    /// <summary>
    /// Verbatim copy of the scalar loop the span-based grayscale pass replaced. Do not modernize
    /// or rewrite this method; it is the behavioral reference the pass must match exactly.
    /// </summary>
    private static void ReferenceApplyGrayscale(byte[] pixels, int width, int height, int stride)
    {
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * stride;

            for (var x = 0; x < width; x++)
            {
                var offset = rowOffset + x * 4;
                var gray = ReferenceGetLuminance(pixels[offset + 2], pixels[offset + 1], pixels[offset]);

                pixels[offset] = gray;
                pixels[offset + 1] = gray;
                pixels[offset + 2] = gray;
            }
        }
    }

    /// <summary>
    /// Verbatim copy of the scalar loop the span-based black-and-white pass replaced. Do not
    /// modernize or rewrite this method; it is the behavioral reference the pass must match
    /// exactly.
    /// </summary>
    private static void ReferenceApplyBlackAndWhite(byte[] pixels, int width, int height, int stride, byte threshold)
    {
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * stride;

            for (var x = 0; x < width; x++)
            {
                var offset = rowOffset + x * 4;
                var gray = ReferenceGetLuminance(pixels[offset + 2], pixels[offset + 1], pixels[offset]);
                var value = gray >= threshold ? byte.MaxValue : byte.MinValue;

                pixels[offset] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
            }
        }
    }

    private static byte ReferenceGetLuminance(byte red, byte green, byte blue)
    {
        return (byte)((red * 299 + green * 587 + blue * 114 + 500) / 1000);
    }
}
