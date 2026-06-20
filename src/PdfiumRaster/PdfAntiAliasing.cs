namespace PdfiumRaster;

[Flags]
public enum PdfAntiAliasing
{
    None = 0,
    Text = 1 << 0,
    Images = 1 << 1,
    Paths = 1 << 2,
    All = Text | Images | Paths,
}