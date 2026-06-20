using System.Runtime.InteropServices;

namespace PdfiumRaster;

public sealed class PdfPage : IDisposable
{
    private const int BitmapFormatBgra = 4;

    private IntPtr _handle;

    internal PdfPage(IntPtr handle)
    {
        _handle = handle;
    }

    public double Width
    {
        get
        {
            ThrowIfDisposed();
            return PdfiumNative.FPDF_GetPageWidthF(_handle);
        }
    }

    public double Height
    {
        get
        {
            ThrowIfDisposed();
            return PdfiumNative.FPDF_GetPageHeightF(_handle);
        }
    }

    public PdfBitmap Render(int width, int height, PdfRenderFlags flags = PdfRenderFlags.Annot)
    {
        var bitmap = PdfBitmap.Create(width, height);
        Render(bitmap, 0, 0, width, height, PdfPageRotation.Normal, flags);
        return bitmap;
    }

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
