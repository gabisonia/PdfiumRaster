using System.Buffers;
using System.Runtime.InteropServices;

namespace PdfiumRaster;

/// <summary>
/// Represents a disposable pooled bitmap buffer for repeated PDF page rendering.
/// </summary>
public sealed class PdfBitmapLease : IDisposable
{
    private byte[]? _pixels;
    private readonly PdfBitmap _bitmap;
    private PdfiumLibrary? _library;
    private GCHandle _pinnedPixels;
    private IntPtr _nativeBitmap;

    private PdfBitmapLease(PdfBitmap bitmap, byte[] pixels)
    {
        _bitmap = bitmap;
        _pixels = pixels;
    }

    /// <summary>
    /// Gets the leased bitmap. The bitmap and its pixel buffer are valid only until this lease is disposed.
    /// </summary>
    public PdfBitmap Bitmap
    {
        get
        {
            if (_pixels is null)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _bitmap;
        }
    }

    /// <summary>
    /// Rents a BGRA bitmap from the shared array pool.
    /// </summary>
    /// <param name="width">Bitmap width in pixels.</param>
    /// <param name="height">Bitmap height in pixels.</param>
    /// <param name="clear">Whether to clear the rendered pixel region before returning the bitmap.</param>
    /// <returns>A disposable lease that returns the bitmap buffer to the shared pool when disposed.</returns>
    /// <remarks>
    /// The leased bitmap may expose a pixel array larger than <c>Stride * Height</c>. Only the rendered pixel region is
    /// owned by the bitmap. Do not retain the bitmap or its <see cref="PdfBitmap.Pixels" /> buffer after disposing the
    /// lease.
    /// </remarks>
    public static PdfBitmapLease Rent(int width, int height, bool clear = true)
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
        var pixelCount = checked(stride * height);
        var pixels = ArrayPool<byte>.Shared.Rent(pixelCount);

        if (clear)
        {
            Array.Clear(pixels, 0, pixelCount);
        }

        return new PdfBitmapLease(new PdfBitmap(width, height, stride, pixels), pixels);
    }

    internal IntPtr GetOrCreateNativeBitmap()
    {
        var pixels = _pixels;
        if (pixels is null)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (_nativeBitmap != IntPtr.Zero)
        {
            return _nativeBitmap;
        }

        try
        {
            _library = PdfiumLibrary.Initialize();
            _pinnedPixels = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            _nativeBitmap = PdfiumNative.FPDFBitmap_CreateEx(
                _bitmap.Width,
                _bitmap.Height,
                PdfiumNative.BitmapFormatBgra,
                _pinnedPixels.AddrOfPinnedObject(),
                _bitmap.Stride);

            if (_nativeBitmap == IntPtr.Zero)
            {
                throw PdfiumException.FromLastError("Could not create reusable PDFium bitmap.");
            }

            return _nativeBitmap;
        }
        catch
        {
            ReleaseNativeBitmap();
            throw;
        }
    }

    /// <summary>
    /// Returns the leased pixel buffer to the shared array pool.
    /// </summary>
    public void Dispose()
    {
        var pixels = _pixels;
        if (pixels is null)
        {
            return;
        }

        _pixels = null;

        try
        {
            ReleaseNativeBitmap();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixels);
        }
    }

    private void ReleaseNativeBitmap()
    {
        try
        {
            if (_nativeBitmap != IntPtr.Zero)
            {
                PdfiumNative.FPDFBitmap_Destroy(_nativeBitmap);
            }
        }
        finally
        {
            _nativeBitmap = IntPtr.Zero;

            try
            {
                if (_pinnedPixels.IsAllocated)
                {
                    _pinnedPixels.Free();
                }
            }
            finally
            {
                _library?.Dispose();
                _library = null;
            }
        }
    }
}
