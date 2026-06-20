namespace PdfiumRaster;

/// <summary>
/// Manages PDFium native library initialization for advanced usage.
/// </summary>
public sealed class PdfiumLibrary : IDisposable
{
    private static readonly object SyncRoot = new();
    private static int _referenceCount;

    private bool _disposed;

    private PdfiumLibrary()
    {
    }

    /// <summary>
    /// Initializes PDFium and returns a handle that releases one initialization reference when disposed.
    /// </summary>
    /// <returns>A disposable initialization handle.</returns>
    public static PdfiumLibrary Initialize()
    {
        lock (SyncRoot)
        {
            if (_referenceCount == 0)
            {
                PdfiumNative.FPDF_InitLibrary();
            }

            _referenceCount++;
        }

        return new PdfiumLibrary();
    }

    /// <summary>
    /// Releases this initialization reference and destroys PDFium when no references remain.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_referenceCount > 0)
            {
                _referenceCount--;
                if (_referenceCount == 0)
                {
                    PdfiumNative.FPDF_DestroyLibrary();
                }
            }
        }

        _disposed = true;
    }
}
