namespace PdfiumRaster;

/// <summary>
/// Represents a rendered PDF page bitmap in BGRA byte order.
/// </summary>
public sealed class PdfBitmap
{
    /// <summary>
    /// Initializes a bitmap from an existing BGRA pixel buffer.
    /// </summary>
    /// <param name="width">Bitmap width in pixels.</param>
    /// <param name="height">Bitmap height in pixels.</param>
    /// <param name="stride">Number of bytes per bitmap row.</param>
    /// <param name="pixels">BGRA pixel buffer.</param>
    public PdfBitmap(int width, int height, int stride, byte[] pixels)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");
        }

        if (stride < checked(width * 4))
        {
            throw new ArgumentOutOfRangeException(nameof(stride), stride, "Stride must fit one BGRA row.");
        }

        if (pixels is null)
        {
            throw new ArgumentNullException(nameof(pixels));
        }

        if (pixels.Length < checked(stride * height))
        {
            throw new ArgumentException("Pixel buffer is smaller than the bitmap dimensions require.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Stride = stride;
        Pixels = pixels;
    }

    /// <summary>
    /// Creates an empty BGRA bitmap with a tightly packed stride.
    /// </summary>
    /// <param name="width">Bitmap width in pixels.</param>
    /// <param name="height">Bitmap height in pixels.</param>
    /// <returns>A new bitmap whose pixel buffer is initialized to zero.</returns>
    public static PdfBitmap Create(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");
        }

        var stride = checked(width * 4);
        return new PdfBitmap(width, height, stride, new byte[checked(stride * height)]);
    }

    /// <summary>
    /// Gets the bitmap width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the bitmap height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the number of bytes per bitmap row.
    /// </summary>
    public int Stride { get; }

    /// <summary>
    /// Pixel data in BGRA byte order.
    /// </summary>
    public byte[] Pixels { get; }
}
