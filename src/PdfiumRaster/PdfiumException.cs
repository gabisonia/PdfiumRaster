namespace PdfiumRaster;

public sealed class PdfiumException : Exception
{
    private PdfiumException(string message, PdfiumError error)
        : base($"{message} PDFium error: {error}.")
    {
        Error = error;
    }

    public PdfiumError Error { get; }

    internal static PdfiumException FromLastError(string message)
    {
        return new PdfiumException(message, (PdfiumError)PdfiumNative.FPDF_GetLastError());
    }
}