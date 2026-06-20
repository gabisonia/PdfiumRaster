namespace PdfiumRaster;

public sealed class PdfiumLibrary : IDisposable
{
    private static readonly object SyncRoot = new();
    private static int _referenceCount;

    private bool _disposed;

    private PdfiumLibrary()
    {
    }

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