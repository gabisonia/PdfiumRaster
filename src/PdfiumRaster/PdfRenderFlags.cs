namespace PdfiumRaster;

/// <summary>
/// PDFium render flags that control rasterization behavior.
/// </summary>
[Flags]
public enum PdfRenderFlags
{
    /// <summary>
    /// Uses PDFium default rendering behavior.
    /// </summary>
    None = 0,

    /// <summary>
    /// Renders annotations.
    /// </summary>
    Annot = 0x01,

    /// <summary>
    /// Optimizes text rendering for LCD displays.
    /// </summary>
    LcdText = 0x02,

    /// <summary>
    /// Disables native text rendering.
    /// </summary>
    NoNativeText = 0x04,

    /// <summary>
    /// Renders in grayscale.
    /// </summary>
    Grayscale = 0x08,

    /// <summary>
    /// Enables PDFium debug rendering information.
    /// </summary>
    DebugInfo = 0x80,

    /// <summary>
    /// Lets PDFium exceptions propagate instead of catching them internally.
    /// </summary>
    NoCatch = 0x100,

    /// <summary>
    /// Limits PDFium image cache usage.
    /// </summary>
    RenderLimitedImageCache = 0x200,

    /// <summary>
    /// Forces halftone rendering.
    /// </summary>
    RenderForceHalftone = 0x400,

    /// <summary>
    /// Uses print-oriented rendering.
    /// </summary>
    PrintMode = 0x800,

    /// <summary>
    /// Disables text smoothing.
    /// </summary>
    RenderNoSmoothText = 0x1000,

    /// <summary>
    /// Disables image smoothing.
    /// </summary>
    RenderNoSmoothImage = 0x2000,

    /// <summary>
    /// Disables path smoothing.
    /// </summary>
    RenderNoSmoothPath = 0x4000,
}
