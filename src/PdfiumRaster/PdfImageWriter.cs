using System.Runtime.InteropServices;
using SkiaSharp;

namespace PdfiumRaster;

public static class PdfImageWriter
{
    private const int BitmapFileHeaderSize = 14;
    private const int BitmapInfoHeaderSize = 40;
    private const int BitsPerPixel = 32;

    public static void SaveBmp(PdfBitmap bitmap, string path)
    {
        if (bitmap is null)
        {
            throw new ArgumentNullException(nameof(bitmap));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        using var stream = File.Create(path);
        WriteBmp(bitmap, stream);
    }

    public static void SavePng(PdfBitmap bitmap, string path)
    {
        SaveEncoded(bitmap, path, SKEncodedImageFormat.Png);
    }

    public static void SaveJpeg(PdfBitmap bitmap, string path, int quality = 100)
    {
        SaveEncoded(bitmap, path, SKEncodedImageFormat.Jpeg, quality);
    }

    public static void SaveWebp(PdfBitmap bitmap, string path, int quality = 100)
    {
        SaveEncoded(bitmap, path, SKEncodedImageFormat.Webp, quality);
    }

    public static void WritePng(PdfBitmap bitmap, Stream stream)
    {
        WriteEncoded(bitmap, stream, SKEncodedImageFormat.Png);
    }

    public static void WriteJpeg(PdfBitmap bitmap, Stream stream, int quality = 100)
    {
        WriteEncoded(bitmap, stream, SKEncodedImageFormat.Jpeg, quality);
    }

    public static void WriteWebp(PdfBitmap bitmap, Stream stream, int quality = 100)
    {
        WriteEncoded(bitmap, stream, SKEncodedImageFormat.Webp, quality);
    }

    public static void WriteBmp(PdfBitmap bitmap, Stream stream)
    {
        if (bitmap is null)
        {
            throw new ArgumentNullException(nameof(bitmap));
        }

        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var pixelDataSize = checked(bitmap.Stride * bitmap.Height);
        var pixelOffset = BitmapFileHeaderSize + BitmapInfoHeaderSize;
        var fileSize = checked(pixelOffset + pixelDataSize);

        var header = new byte[pixelOffset];

        header[0] = (byte)'B';
        header[1] = (byte)'M';
        WriteInt32(header, 2, fileSize);
        WriteInt32(header, 10, pixelOffset);
        WriteInt32(header, 14, BitmapInfoHeaderSize);
        WriteInt32(header, 18, bitmap.Width);
        WriteInt32(header, 22, -bitmap.Height);
        WriteUInt16(header, 26, 1);
        WriteUInt16(header, 28, BitsPerPixel);
        WriteInt32(header, 34, pixelDataSize);

        stream.Write(header, 0, header.Length);

        for (var row = 0; row < bitmap.Height; row++)
        {
            stream.Write(bitmap.Pixels, row * bitmap.Stride, bitmap.Stride);
        }
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteUInt16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    private static void SaveEncoded(PdfBitmap bitmap, string path, SKEncodedImageFormat format, int quality = 100)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        using var stream = File.Create(path);
        WriteEncoded(bitmap, stream, format, quality);
    }

    private static void WriteEncoded(PdfBitmap bitmap, Stream stream, SKEncodedImageFormat format, int quality = 100)
    {
        if (bitmap is null)
        {
            throw new ArgumentNullException(nameof(bitmap));
        }

        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var skBitmap = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var pixels = skBitmap.GetPixels();

        if (skBitmap.RowBytes == bitmap.Stride)
        {
            Marshal.Copy(bitmap.Pixels, 0, pixels, bitmap.Stride * bitmap.Height);
        }
        else
        {
            for (var row = 0; row < bitmap.Height; row++)
            {
                Marshal.Copy(bitmap.Pixels, row * bitmap.Stride, pixels + row * skBitmap.RowBytes, bitmap.Stride);
            }
        }

        using var image = SKImage.FromBitmap(skBitmap);
        using var data = image.Encode(format, quality);
        data.SaveTo(stream);
    }
}
