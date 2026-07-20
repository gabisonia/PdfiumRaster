namespace PdfiumRaster;

internal sealed class PdfNativeBitmapLease : IDisposable
{
    private PdfiumLibrary? _library;
    private IntPtr _handle;
    private IntPtr _pixels;

    private PdfNativeBitmapLease(
        PdfiumLibrary library,
        IntPtr handle,
        IntPtr pixels,
        int width,
        int height,
        int stride)
    {
        _library = library;
        _handle = handle;
        _pixels = pixels;
        Width = width;
        Height = height;
        Stride = stride;
    }

    internal int Width { get; }

    internal int Height { get; }

    internal int Stride { get; }

    internal IntPtr Pixels
    {
        get
        {
            ThrowIfDisposed();
            return _pixels;
        }
    }

    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    internal int PixelDataSize => checked(Stride * Height);

    internal unsafe void Clear()
    {
        new Span<byte>((void*)Pixels, PixelDataSize).Clear();
    }

    internal static PdfNativeBitmapLease Create(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");
        }

        checked
        {
            _ = width * 4;
        }

        var library = PdfiumLibrary.Initialize();
        var handle = IntPtr.Zero;

        try
        {
            handle = PdfiumNative.FPDFBitmap_CreateEx(
                width,
                height,
                PdfiumNative.BitmapFormatBgra,
                IntPtr.Zero,
                0);

            if (handle == IntPtr.Zero)
            {
                throw PdfiumException.FromLastError("Could not create PDFium-owned bitmap.");
            }

            var pixels = PdfiumNative.FPDFBitmap_GetBuffer(handle);
            if (pixels == IntPtr.Zero)
            {
                throw new InvalidOperationException("PDFium returned a bitmap without a pixel buffer.");
            }

            var stride = PdfiumNative.FPDFBitmap_GetStride(handle);
            if (stride < checked(width * 4))
            {
                throw new InvalidOperationException("PDFium returned a bitmap stride smaller than one BGRA row.");
            }

            _ = checked(stride * height);
            return new PdfNativeBitmapLease(library, handle, pixels, width, height, stride);
        }
        catch
        {
            if (handle != IntPtr.Zero)
            {
                PdfiumNative.FPDFBitmap_Destroy(handle);
            }

            library.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            return;
        }

        _pixels = IntPtr.Zero;

        try
        {
            PdfiumNative.FPDFBitmap_Destroy(handle);
        }
        finally
        {
            _library?.Dispose();
            _library = null;
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
