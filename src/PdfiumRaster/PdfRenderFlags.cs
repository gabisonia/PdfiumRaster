namespace PdfiumRaster;

[Flags]
public enum PdfRenderFlags
{
    None = 0,
    Annot = 0x01,
    LcdText = 0x02,
    NoNativeText = 0x04,
    Grayscale = 0x08,
    DebugInfo = 0x80,
    NoCatch = 0x100,
    RenderLimitedImageCache = 0x200,
    RenderForceHalftone = 0x400,
    PrintMode = 0x800,
    RenderNoSmoothText = 0x1000,
    RenderNoSmoothImage = 0x2000,
    RenderNoSmoothPath = 0x4000,
}