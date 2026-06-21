using System.Runtime.InteropServices;

namespace PdfiumRaster;

/// <summary>
/// Represents an open PDF page.
/// </summary>
public sealed class PdfPage : IDisposable
{
    private const int BitmapFormatBgra = 4;

    private IntPtr _handle;

    internal PdfPage(IntPtr handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Gets the page width in PDF points.
    /// </summary>
    public double Width
    {
        get
        {
            ThrowIfDisposed();
            return PdfiumNative.FPDF_GetPageWidthF(_handle);
        }
    }

    /// <summary>
    /// Gets the page height in PDF points.
    /// </summary>
    public double Height
    {
        get
        {
            ThrowIfDisposed();
            return PdfiumNative.FPDF_GetPageHeightF(_handle);
        }
    }

    /// <summary>
    /// Renders the page to a bitmap with an explicit pixel size.
    /// </summary>
    /// <param name="width">Output width in pixels.</param>
    /// <param name="height">Output height in pixels.</param>
    /// <param name="flags">PDFium render flags.</param>
    /// <returns>The rendered page bitmap.</returns>
    public PdfBitmap Render(int width, int height, PdfRenderFlags flags = PdfRenderFlags.Annot)
    {
        var bitmap = PdfBitmap.Create(width, height);
        Render(bitmap, 0, 0, width, height, PdfPageRotation.Normal, flags);
        return bitmap;
    }

    /// <summary>
    /// Renders the page to a bitmap using render options.
    /// </summary>
    /// <param name="options">Optional render options.</param>
    /// <returns>The rendered page bitmap.</returns>
    public PdfBitmap Render(PdfPageRenderOptions? options = null)
    {
        ThrowIfDisposed();

        options ??= new PdfPageRenderOptions();
        var (width, height) = options.GetPixelSize(Width, Height);
        var bitmap = PdfBitmap.Create(width, height);

        Render(
            bitmap,
            0,
            0,
            width,
            height,
            options.Rotation,
            options.GetRenderFlags(),
            options.FillBackground ? options.BackgroundColor : null);

        return bitmap;
    }

    /// <summary>
    /// Renders the page into an existing bitmap using render options.
    /// </summary>
    /// <param name="bitmap">Destination bitmap whose dimensions must match the configured render size.</param>
    /// <param name="options">Optional render options.</param>
    public void Render(PdfBitmap bitmap, PdfPageRenderOptions? options = null)
    {
        ThrowIfDisposed();
        if (bitmap is null)
        {
            throw new ArgumentNullException(nameof(bitmap));
        }

        options ??= new PdfPageRenderOptions();
        var (width, height) = options.GetPixelSize(Width, Height);

        if (bitmap.Width != width || bitmap.Height != height)
        {
            throw new ArgumentException(
                $"Destination bitmap must be {width}x{height} pixels for the requested render options.",
                nameof(bitmap));
        }

        Render(
            bitmap,
            0,
            0,
            width,
            height,
            options.Rotation,
            options.GetRenderFlags(),
            options.FillBackground ? options.BackgroundColor : null);
    }

    /// <summary>
    /// Renders the page into an existing bitmap.
    /// </summary>
    /// <param name="bitmap">Destination bitmap.</param>
    /// <param name="startX">Horizontal offset in destination pixels.</param>
    /// <param name="startY">Vertical offset in destination pixels.</param>
    /// <param name="sizeX">Rendered width in destination pixels.</param>
    /// <param name="sizeY">Rendered height in destination pixels.</param>
    /// <param name="rotate">Page rotation.</param>
    /// <param name="flags">PDFium render flags.</param>
    /// <param name="backgroundColor">Optional ARGB background color used to fill the bitmap before rendering.</param>
    public void Render(
        PdfBitmap bitmap,
        int startX,
        int startY,
        int sizeX,
        int sizeY,
        PdfPageRotation rotate = PdfPageRotation.Normal,
        PdfRenderFlags flags = PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
        uint? backgroundColor = 0xFFFFFFFF)
    {
        ThrowIfDisposed();
        if (bitmap is null)
        {
            throw new ArgumentNullException(nameof(bitmap));
        }

        if (sizeX <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeX), sizeX, "Rendered width must be greater than zero.");
        }

        if (sizeY <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeY), sizeY, "Rendered height must be greater than zero.");
        }

        if (!Enum.IsDefined(typeof(PdfPageRotation), rotate))
        {
            throw new ArgumentOutOfRangeException(nameof(rotate), rotate, "Rotation must be a defined page rotation.");
        }

        var pinnedPixels = GCHandle.Alloc(bitmap.Pixels, GCHandleType.Pinned);

        try
        {
            var nativeBitmap = PdfiumNative.FPDFBitmap_CreateEx(
                bitmap.Width,
                bitmap.Height,
                BitmapFormatBgra,
                pinnedPixels.AddrOfPinnedObject(),
                bitmap.Stride);

            if (nativeBitmap == IntPtr.Zero)
            {
                throw PdfiumException.FromLastError("Could not create PDFium bitmap.");
            }

            try
            {
                if (backgroundColor.HasValue)
                {
                    PdfiumNative.FPDFBitmap_FillRect(nativeBitmap, 0, 0, bitmap.Width, bitmap.Height,
                        backgroundColor.Value);
                }

                PdfiumNative.FPDF_RenderPageBitmap(nativeBitmap, _handle, startX, startY, sizeX, sizeY, (int)rotate,
                    flags);
            }
            finally
            {
                PdfiumNative.FPDFBitmap_Destroy(nativeBitmap);
            }
        }
        finally
        {
            pinnedPixels.Free();
        }
    }

    /// <summary>
    /// Closes the page and releases associated native resources.
    /// </summary>
    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero)
        {
            PdfiumNative.FPDF_ClosePage(handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
