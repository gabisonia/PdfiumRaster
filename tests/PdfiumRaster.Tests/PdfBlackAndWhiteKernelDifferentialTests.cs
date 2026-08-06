using PdfiumRaster;

namespace PdfiumRaster.Tests;

/// <summary>
/// Compares the black-and-white-from-grayscale kernel byte for byte against the scalar loop it
/// replaced, across widths that exercise every alignment, packed and padded strides, and every
/// threshold edge. The full buffer is compared, including padding bytes.
/// </summary>
public sealed class PdfBlackAndWhiteKernelDifferentialTests
{
    private const int RandomSeed = 987654;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(254)]
    [InlineData(255)]
    public void Kernel_matches_reference_scalar_loop(byte threshold)
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
                ReferenceApplyBlackAndWhiteFromGrayscale(expected, width, height, stride, threshold);

                var bitmap = new PdfBitmap(width, height, stride, pixels);
                PdfImageConverter.ApplyConversionColorMode(bitmap, new PdfImageConversionOptions
                {
                    ColorMode = PdfImageColorMode.BlackAndWhite,
                    BlackAndWhiteThreshold = threshold,
                });

                Assert.True(
                    expected.AsSpan().SequenceEqual(bitmap.Pixels),
                    $"Kernel output diverged from the reference loop for width {width}, " +
                    $"padding {paddingBytes}, threshold {threshold}.");
            }
        }
    }

    /// <summary>
    /// Verbatim copy of the scalar loop the kernel replaced. Do not modernize or rewrite this
    /// method; it is the behavioral reference the kernel must match exactly.
    /// </summary>
    private static void ReferenceApplyBlackAndWhiteFromGrayscale(
        byte[] pixels,
        int width,
        int height,
        int stride,
        byte threshold)
    {
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * stride;

            for (var x = 0; x < width; x++)
            {
                var offset = rowOffset + x * 4;
                var value = pixels[offset] >= threshold ? byte.MaxValue : byte.MinValue;

                pixels[offset] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
            }
        }
    }
}
