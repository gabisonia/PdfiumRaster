namespace PdfiumRaster;

public sealed class PdfBitmap
{
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

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    /// <summary>
    /// Pixel data in BGRA byte order.
    /// </summary>
    public byte[] Pixels { get; }
}