using System.Runtime.InteropServices;

namespace PdfiumRaster;

/// <summary>
/// Represents an open PDF document.
/// </summary>
public sealed class PdfDocument : IDisposable
{
    private IntPtr _handle;
    private GCHandle _pinnedBytes;

    private PdfDocument(IntPtr handle, GCHandle pinnedBytes = default)
    {
        _handle = handle;
        _pinnedBytes = pinnedBytes;
    }

    /// <summary>
    /// Gets the number of pages in the document.
    /// </summary>
    public int PageCount
    {
        get
        {
            ThrowIfDisposed();
            return PdfiumNative.FPDF_GetPageCount(_handle);
        }
    }

    /// <summary>
    /// Opens a PDF document from a file path.
    /// </summary>
    /// <param name="path">Path to the PDF file.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>An open PDF document.</returns>
    public static PdfDocument Load(string path, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        var handle = PdfiumNative.FPDF_LoadDocument(path, password);
        if (handle == IntPtr.Zero)
        {
            throw PdfiumException.FromLastError($"Could not load PDF document '{path}'.");
        }

        return new PdfDocument(handle);
    }

    /// <summary>
    /// Opens a PDF document from a byte array.
    /// </summary>
    /// <param name="bytes">PDF file bytes.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>An open PDF document.</returns>
    public static PdfDocument Load(byte[] bytes, string? password = null)
    {
        if (bytes is null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        if (bytes.Length == 0)
        {
            throw new ArgumentException("PDF bytes cannot be empty.", nameof(bytes));
        }

        var pinnedBytes = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        var handle = PdfiumNative.FPDF_LoadMemDocument(pinnedBytes.AddrOfPinnedObject(), bytes.Length, password);

        if (handle == IntPtr.Zero)
        {
            pinnedBytes.Free();
            throw PdfiumException.FromLastError("Could not load PDF document from memory.");
        }

        return new PdfDocument(handle, pinnedBytes);
    }

    /// <summary>
    /// Opens a PDF document from a stream.
    /// </summary>
    /// <param name="stream">Stream containing PDF file data.</param>
    /// <param name="leaveOpen">Whether to leave <paramref name="stream"/> open after loading.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>An open PDF document.</returns>
    public static PdfDocument Load(Stream stream, bool leaveOpen = false, string? password = null)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        try
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return Load(memoryStream.ToArray(), password);
        }
        finally
        {
            if (!leaveOpen)
            {
                stream.Dispose();
            }
        }
    }

    /// <summary>
    /// Loads a single page by zero-based page index.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <returns>An open PDF page.</returns>
    public PdfPage LoadPage(int pageIndex)
    {
        ThrowIfDisposed();

        if (pageIndex < 0 || pageIndex >= PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex,
                "Page index is outside the document page range.");
        }

        var pageHandle = PdfiumNative.FPDF_LoadPage(_handle, pageIndex);
        if (pageHandle == IntPtr.Zero)
        {
            throw PdfiumException.FromLastError($"Could not load PDF page {pageIndex}.");
        }

        return new PdfPage(pageHandle);
    }

    /// <summary>
    /// Closes the document and releases associated native resources.
    /// </summary>
    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero)
        {
            PdfiumNative.FPDF_CloseDocument(handle);
        }

        if (_pinnedBytes.IsAllocated)
        {
            _pinnedBytes.Free();
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
