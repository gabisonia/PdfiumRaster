using System.Runtime.InteropServices;
using PdfiumRaster;

namespace PdfiumRaster.Tests;

/// <summary>
/// Pins the exact behavior of the color-mode post-processing passes before they are optimized:
/// alpha bytes are preserved, stride padding bytes are never written, thresholding uses
/// greater-than-or-equal semantics at every edge, and black-and-white uses the same luminance
/// computation as grayscale.
/// </summary>
public sealed class PdfColorModePinningTests
{
    private const int RandomSeed = 12345;

    [Fact]
    public void Conversion_black_and_white_preserves_alpha_on_managed_bitmap()
    {
        const int width = 13;
        const int height = 7;
        var bitmap = CreateRandomGrayscaleBitmap(width, height, stride: width * 4, out var original);

        PdfImageConverter.ApplyConversionColorMode(bitmap, BlackAndWhiteOptions(threshold: 128));

        for (var offset = 3; offset < bitmap.Pixels.Length; offset += 4)
        {
            Assert.Equal(original[offset], bitmap.Pixels[offset]);
        }
    }

    [Fact]
    public void Conversion_black_and_white_thresholds_blue_channel_on_managed_bitmap()
    {
        const int width = 13;
        const int height = 7;
        const byte threshold = 128;
        var bitmap = CreateRandomGrayscaleBitmap(width, height, stride: width * 4, out var original);

        PdfImageConverter.ApplyConversionColorMode(bitmap, BlackAndWhiteOptions(threshold));

        for (var offset = 0; offset < bitmap.Pixels.Length; offset += 4)
        {
            var expected = original[offset] >= threshold ? byte.MaxValue : byte.MinValue;
            Assert.Equal(expected, bitmap.Pixels[offset]);
            Assert.Equal(expected, bitmap.Pixels[offset + 1]);
            Assert.Equal(expected, bitmap.Pixels[offset + 2]);
        }
    }

    [Fact]
    public void Conversion_black_and_white_preserves_alpha_and_padding_on_native_lease()
    {
        const int width = 13;
        const int height = 7;
        const byte threshold = 128;

        using var lease = PdfNativeBitmapLease.Create(width, height);
        var original = FillNativeLeaseWithRandomGrayscale(lease);

        PdfImageConverter.ApplyConversionColorMode(lease, BlackAndWhiteOptions(threshold));

        var actual = new byte[lease.PixelDataSize];
        Marshal.Copy(lease.Pixels, actual, 0, actual.Length);

        for (var y = 0; y < height; y++)
        {
            var row = y * lease.Stride;

            for (var x = 0; x < width; x++)
            {
                var offset = row + x * 4;
                var expected = original[offset] >= threshold ? byte.MaxValue : byte.MinValue;
                Assert.Equal(expected, actual[offset]);
                Assert.Equal(expected, actual[offset + 1]);
                Assert.Equal(expected, actual[offset + 2]);
                Assert.Equal(original[offset + 3], actual[offset + 3]);
            }

            for (var padding = width * 4; padding < lease.Stride; padding++)
            {
                Assert.Equal(original[row + padding], actual[row + padding]);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(12)]
    public void Conversion_black_and_white_leaves_stride_padding_untouched(int paddingBytes)
    {
        const int width = 5;
        const int height = 4;
        const byte sentinel = 0xAB;
        var stride = width * 4 + paddingBytes;
        var bitmap = CreateRandomGrayscaleBitmap(width, height, stride, out _, paddingFill: sentinel);

        PdfImageConverter.ApplyConversionColorMode(bitmap, BlackAndWhiteOptions(threshold: 128));

        AssertPaddingEquals(bitmap, sentinel);
    }

    [Theory]
    [InlineData(PdfImageColorMode.Grayscale, 1)]
    [InlineData(PdfImageColorMode.Grayscale, 12)]
    [InlineData(PdfImageColorMode.BlackAndWhite, 1)]
    [InlineData(PdfImageColorMode.BlackAndWhite, 12)]
    public void ApplyColorMode_leaves_stride_padding_untouched(PdfImageColorMode colorMode, int paddingBytes)
    {
        const int width = 5;
        const int height = 4;
        const byte sentinel = 0xAB;
        var stride = width * 4 + paddingBytes;
        var bitmap = CreateRandomColorBitmap(width, height, stride, paddingFill: sentinel);

        PdfImageConverter.ApplyColorMode(bitmap, colorMode, blackAndWhiteThreshold: 128);

        AssertPaddingEquals(bitmap, sentinel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(254)]
    [InlineData(255)]
    public void Conversion_black_and_white_uses_greater_or_equal_threshold(byte threshold)
    {
        var grays = GetThresholdEdgeGrays(threshold);
        var bitmap = CreateGrayscaleRow(grays);

        PdfImageConverter.ApplyConversionColorMode(bitmap, BlackAndWhiteOptions(threshold));

        AssertThresholdedRow(bitmap, grays, threshold);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(254)]
    [InlineData(255)]
    public void ApplyColorMode_black_and_white_uses_greater_or_equal_threshold(byte threshold)
    {
        var grays = GetThresholdEdgeGrays(threshold);
        var bitmap = CreateGrayscaleRow(grays);

        PdfImageConverter.ApplyColorMode(bitmap, PdfImageColorMode.BlackAndWhite, threshold);

        AssertThresholdedRow(bitmap, grays, threshold);
    }

    [Fact]
    public void ApplyColorMode_black_and_white_thresholds_the_grayscale_luminance()
    {
        // (B=10, G=20, R=30) has luminance 22, matching the pinned grayscale conversion:
        // threshold 22 keeps the pixel white and threshold 23 turns it black.
        var atThreshold = new PdfBitmap(width: 1, height: 1, stride: 4, pixels: [10, 20, 30, 200]);
        var aboveThreshold = new PdfBitmap(width: 1, height: 1, stride: 4, pixels: [10, 20, 30, 200]);

        PdfImageConverter.ApplyColorMode(atThreshold, PdfImageColorMode.BlackAndWhite, blackAndWhiteThreshold: 22);
        PdfImageConverter.ApplyColorMode(aboveThreshold, PdfImageColorMode.BlackAndWhite, blackAndWhiteThreshold: 23);

        Assert.Equal([255, 255, 255, 200], atThreshold.Pixels);
        Assert.Equal([0, 0, 0, 200], aboveThreshold.Pixels);
    }

    [Fact]
    public void ApplyColorMode_black_and_white_preserves_alpha()
    {
        const int width = 13;
        const int height = 7;
        var bitmap = CreateRandomColorBitmap(width, height, stride: width * 4);
        var original = (byte[])bitmap.Pixels.Clone();

        PdfImageConverter.ApplyColorMode(bitmap, PdfImageColorMode.BlackAndWhite, blackAndWhiteThreshold: 128);

        for (var offset = 3; offset < bitmap.Pixels.Length; offset += 4)
        {
            Assert.Equal(original[offset], bitmap.Pixels[offset]);
        }
    }

    [Fact]
    public void ApplyColorMode_grayscale_preserves_alpha()
    {
        const int width = 13;
        const int height = 7;
        var bitmap = CreateRandomColorBitmap(width, height, stride: width * 4);
        var original = (byte[])bitmap.Pixels.Clone();

        PdfImageConverter.ApplyColorMode(bitmap, PdfImageColorMode.Grayscale);

        for (var offset = 3; offset < bitmap.Pixels.Length; offset += 4)
        {
            Assert.Equal(original[offset], bitmap.Pixels[offset]);
        }
    }

    private static PdfImageConversionOptions BlackAndWhiteOptions(byte threshold)
    {
        return new PdfImageConversionOptions
        {
            ColorMode = PdfImageColorMode.BlackAndWhite,
            BlackAndWhiteThreshold = threshold,
        };
    }

    private static PdfBitmap CreateRandomGrayscaleBitmap(
        int width,
        int height,
        int stride,
        out byte[] original,
        byte? paddingFill = null)
    {
        var random = new Random(RandomSeed);
        var pixels = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            var row = y * stride;

            for (var x = 0; x < width; x++)
            {
                var offset = row + x * 4;
                var gray = (byte)random.Next(256);
                pixels[offset] = gray;
                pixels[offset + 1] = gray;
                pixels[offset + 2] = gray;
                pixels[offset + 3] = (byte)random.Next(256);
            }

            if (paddingFill.HasValue)
            {
                for (var padding = width * 4; padding < stride; padding++)
                {
                    pixels[row + padding] = paddingFill.Value;
                }
            }
        }

        original = (byte[])pixels.Clone();
        return new PdfBitmap(width, height, stride, pixels);
    }

    private static PdfBitmap CreateRandomColorBitmap(int width, int height, int stride, byte? paddingFill = null)
    {
        var random = new Random(RandomSeed);
        var pixels = new byte[stride * height];
        random.NextBytes(pixels);

        if (paddingFill.HasValue)
        {
            for (var y = 0; y < height; y++)
            {
                var row = y * stride;

                for (var padding = width * 4; padding < stride; padding++)
                {
                    pixels[row + padding] = paddingFill.Value;
                }
            }
        }

        return new PdfBitmap(width, height, stride, pixels);
    }

    private static byte[] FillNativeLeaseWithRandomGrayscale(PdfNativeBitmapLease lease)
    {
        var random = new Random(RandomSeed);
        var pixels = new byte[lease.PixelDataSize];
        random.NextBytes(pixels);

        for (var y = 0; y < lease.Height; y++)
        {
            var row = y * lease.Stride;

            for (var x = 0; x < lease.Width; x++)
            {
                var offset = row + x * 4;
                pixels[offset + 1] = pixels[offset];
                pixels[offset + 2] = pixels[offset];
            }
        }

        Marshal.Copy(pixels, 0, lease.Pixels, pixels.Length);
        return pixels;
    }

    private static byte[] GetThresholdEdgeGrays(byte threshold)
    {
        return new[] { 0, threshold - 1, (int)threshold, threshold + 1, 255 }
            .Where(gray => gray is >= 0 and <= 255)
            .Distinct()
            .Select(gray => (byte)gray)
            .ToArray();
    }

    private static PdfBitmap CreateGrayscaleRow(byte[] grays)
    {
        var pixels = new byte[grays.Length * 4];

        for (var x = 0; x < grays.Length; x++)
        {
            pixels[x * 4] = grays[x];
            pixels[x * 4 + 1] = grays[x];
            pixels[x * 4 + 2] = grays[x];
            pixels[x * 4 + 3] = 200;
        }

        return new PdfBitmap(grays.Length, height: 1, stride: grays.Length * 4, pixels);
    }

    private static void AssertThresholdedRow(PdfBitmap bitmap, byte[] grays, byte threshold)
    {
        for (var x = 0; x < grays.Length; x++)
        {
            var offset = x * 4;
            var expected = grays[x] >= threshold ? byte.MaxValue : byte.MinValue;
            Assert.Equal(expected, bitmap.Pixels[offset]);
            Assert.Equal(expected, bitmap.Pixels[offset + 1]);
            Assert.Equal(expected, bitmap.Pixels[offset + 2]);
            Assert.Equal(200, bitmap.Pixels[offset + 3]);
        }
    }

    private static void AssertPaddingEquals(PdfBitmap bitmap, byte sentinel)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            var row = y * bitmap.Stride;

            for (var padding = bitmap.Width * 4; padding < bitmap.Stride; padding++)
            {
                Assert.Equal(sentinel, bitmap.Pixels[row + padding]);
            }
        }
    }
}
