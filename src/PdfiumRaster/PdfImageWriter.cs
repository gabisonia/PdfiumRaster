using System.Runtime.InteropServices;
using SkiaSharp;

namespace PdfiumRaster;

/// <summary>
/// Writes rendered PDF bitmaps to image files or streams.
/// </summary>
public static class PdfImageWriter
{
    private const int BitmapFileHeaderSize = 14;
    private const int BitmapInfoHeaderSize = 40;
    private const int BitsPerPixel = 32;

    /// <summary>
    /// Saves a bitmap as a BMP file.
    /// </summary>
    /// <param name="bitmap">Bitmap to save.</param>
    /// <param name="path">Destination file path.</param>
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

    /// <summary>
    /// Saves a bitmap as a PNG file.
    /// </summary>
    /// <param name="bitmap">Bitmap to save.</param>
    /// <param name="path">Destination file path.</param>
    public static void SavePng(PdfBitmap bitmap, string path)
    {
        SavePng(bitmap, path, new PdfImageEncodingOptions());
    }

    /// <summary>
    /// Saves a bitmap as a PNG file.
    /// </summary>
    /// <param name="bitmap">Bitmap to save.</param>
    /// <param name="path">Destination file path.</param>
    /// <param name="options">PNG encoding settings.</param>
    public static void SavePng(PdfBitmap bitmap, string path, PdfImageEncodingOptions options)
    {
        SaveEncoded(bitmap, path, SKEncodedImageFormat.Png, options);
    }

    /// <summary>
    /// Saves a bitmap as a JPEG file.
    /// </summary>
    /// <param name="bitmap">Bitmap to save.</param>
    /// <param name="path">Destination file path.</param>
    /// <param name="quality">Encoder quality from 0 to 100.</param>
    public static void SaveJpeg(PdfBitmap bitmap, string path, int quality = 100)
    {
        SaveJpeg(bitmap, path, new PdfImageEncodingOptions { Quality = quality });
    }

    /// <summary>
    /// Saves a bitmap as a JPEG file.
    /// </summary>
    /// <param name="bitmap">Bitmap to save.</param>
    /// <param name="path">Destination file path.</param>
    /// <param name="options">JPEG encoding settings.</param>
    public static void SaveJpeg(PdfBitmap bitmap, string path, PdfImageEncodingOptions options)
    {
        SaveEncoded(bitmap, path, SKEncodedImageFormat.Jpeg, options);
    }

    /// <summary>
    /// Saves a bitmap as a WebP file.
    /// </summary>
    /// <param name="bitmap">Bitmap to save.</param>
    /// <param name="path">Destination file path.</param>
    /// <param name="quality">Encoder quality from 0 to 100.</param>
    public static void SaveWebp(PdfBitmap bitmap, string path, int quality = 100)
    {
        SaveWebp(bitmap, path, new PdfImageEncodingOptions { Quality = quality });
    }

    /// <summary>
    /// Saves a bitmap as a WebP file.
    /// </summary>
    /// <param name="bitmap">Bitmap to save.</param>
    /// <param name="path">Destination file path.</param>
    /// <param name="options">WebP encoding settings.</param>
    public static void SaveWebp(PdfBitmap bitmap, string path, PdfImageEncodingOptions options)
    {
        SaveEncoded(bitmap, path, SKEncodedImageFormat.Webp, options);
    }

    /// <summary>
    /// Writes a bitmap as PNG to a stream.
    /// </summary>
    /// <param name="bitmap">Bitmap to write.</param>
    /// <param name="stream">Destination stream.</param>
    public static void WritePng(PdfBitmap bitmap, Stream stream)
    {
        WritePng(bitmap, stream, new PdfImageEncodingOptions());
    }

    /// <summary>
    /// Writes a bitmap as PNG to a stream without closing the stream.
    /// </summary>
    /// <param name="bitmap">Bitmap to write.</param>
    /// <param name="stream">Destination stream, which remains open.</param>
    /// <param name="options">PNG encoding settings.</param>
    public static void WritePng(PdfBitmap bitmap, Stream stream, PdfImageEncodingOptions options)
    {
        WriteEncoded(bitmap, stream, SKEncodedImageFormat.Png, options);
    }

    /// <summary>
    /// Writes a bitmap as JPEG to a stream.
    /// </summary>
    /// <param name="bitmap">Bitmap to write.</param>
    /// <param name="stream">Destination stream.</param>
    /// <param name="quality">Encoder quality from 0 to 100.</param>
    public static void WriteJpeg(PdfBitmap bitmap, Stream stream, int quality = 100)
    {
        WriteJpeg(bitmap, stream, new PdfImageEncodingOptions { Quality = quality });
    }

    /// <summary>
    /// Writes a bitmap as JPEG to a stream without closing the stream.
    /// </summary>
    /// <param name="bitmap">Bitmap to write.</param>
    /// <param name="stream">Destination stream, which remains open.</param>
    /// <param name="options">JPEG encoding settings.</param>
    public static void WriteJpeg(PdfBitmap bitmap, Stream stream, PdfImageEncodingOptions options)
    {
        WriteEncoded(bitmap, stream, SKEncodedImageFormat.Jpeg, options);
    }

    /// <summary>
    /// Writes a bitmap as WebP to a stream.
    /// </summary>
    /// <param name="bitmap">Bitmap to write.</param>
    /// <param name="stream">Destination stream.</param>
    /// <param name="quality">Encoder quality from 0 to 100.</param>
    public static void WriteWebp(PdfBitmap bitmap, Stream stream, int quality = 100)
    {
        WriteWebp(bitmap, stream, new PdfImageEncodingOptions { Quality = quality });
    }

    /// <summary>
    /// Writes a bitmap as WebP to a stream without closing the stream.
    /// </summary>
    /// <param name="bitmap">Bitmap to write.</param>
    /// <param name="stream">Destination stream, which remains open.</param>
    /// <param name="options">WebP encoding settings.</param>
    public static void WriteWebp(PdfBitmap bitmap, Stream stream, PdfImageEncodingOptions options)
    {
        WriteEncoded(bitmap, stream, SKEncodedImageFormat.Webp, options);
    }

    /// <summary>
    /// Writes a bitmap as BMP to a stream.
    /// </summary>
    /// <param name="bitmap">Bitmap to write.</param>
    /// <param name="stream">Destination stream.</param>
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

    private static void SaveEncoded(
        PdfBitmap bitmap,
        string path,
        SKEncodedImageFormat format,
        PdfImageEncodingOptions options)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        using var stream = File.Create(path);
        WriteEncoded(bitmap, stream, format, options);
    }

    private static void WriteEncoded(
        PdfBitmap bitmap,
        Stream stream,
        SKEncodedImageFormat format,
        PdfImageEncodingOptions options)
    {
        if (bitmap is null)
        {
            throw new ArgumentNullException(nameof(bitmap));
        }

        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var pinnedPixels = GCHandle.Alloc(bitmap.Pixels, GCHandleType.Pinned);

        try
        {
            var imageInfo = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            using var pixmap = new SKPixmap(imageInfo, pinnedPixels.AddrOfPinnedObject(), bitmap.Stride);
            var encoded = format switch
            {
                SKEncodedImageFormat.Png => EncodePng(pixmap, stream, options),
                SKEncodedImageFormat.Jpeg => pixmap.Encode(stream, new SKJpegEncoderOptions(options.Quality)),
                SKEncodedImageFormat.Webp => pixmap.Encode(stream,
                    new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, options.Quality)),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Image format is not supported."),
            };

            if (!encoded)
            {
                throw new InvalidOperationException("Could not encode bitmap image.");
            }
        }
        finally
        {
            pinnedPixels.Free();
        }
    }

    private static bool EncodePng(SKPixmap pixmap, Stream stream, PdfImageEncodingOptions options)
    {
        var pngOptions = options.PngCompressionLevel.HasValue
            ? new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, options.PngCompressionLevel.Value)
            : SKPngEncoderOptions.Default;

        return pixmap.Encode(stream, pngOptions);
    }
}
