namespace PdfiumRaster;

/// <summary>
/// Exception thrown when PDFium reports a native error.
/// </summary>
public sealed class PdfiumException : Exception
{
    private PdfiumException(string message, PdfiumError error)
        : base($"{message} PDFium error: {error}.")
    {
        Error = error;
    }

    /// <summary>
    /// Gets the PDFium error code.
    /// </summary>
    public PdfiumError Error { get; }

    internal static PdfiumException FromLastError(string message)
    {
        return new PdfiumException(message, (PdfiumError)PdfiumNative.FPDF_GetLastError());
    }
}
