namespace PdfiumRaster;

/// <summary>
/// PDFium error codes returned by the native library.
/// </summary>
public enum PdfiumError : uint
{
    /// <summary>
    /// No error was reported.
    /// </summary>
    Success = 0,

    /// <summary>
    /// An unknown error occurred.
    /// </summary>
    Unknown = 1,

    /// <summary>
    /// The file could not be found or opened.
    /// </summary>
    File = 2,

    /// <summary>
    /// The file format is invalid or unsupported.
    /// </summary>
    Format = 3,

    /// <summary>
    /// The document password is missing or incorrect.
    /// </summary>
    Password = 4,

    /// <summary>
    /// The document security handler is unsupported.
    /// </summary>
    Security = 5,

    /// <summary>
    /// A page-related error occurred.
    /// </summary>
    Page = 6,
}
