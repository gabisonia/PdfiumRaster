# PdfiumRaster API Reference

This document summarizes the supported public API surface.

For project structure, runtime architecture, native dependency behavior, and contributor guidance, see [Architecture](ARCHITECTURE.md).

For memory and benchmark guidance, see [Performance](PERFORMANCE.md).

## Primary Facade

Use `PdfImageConverter` for most workflows.

### Page Metadata

```csharp
int pageCount = PdfImageConverter.GetPageCount("input.pdf");
IReadOnlyList<PdfPageSize> sizes = PdfImageConverter.GetPageSizes("input.pdf");
```

Supported inputs:

```csharp
PdfImageConverter.GetPageCount(string pdfPath);
PdfImageConverter.GetPageCount(byte[] pdfBytes);
PdfImageConverter.GetPageCount(Stream pdfStream);
PdfImageConverter.GetPageCountFromBase64(string pdfBase64);

PdfImageConverter.GetPageSizes(string pdfPath);
PdfImageConverter.GetPageSizes(byte[] pdfBytes);
PdfImageConverter.GetPageSizes(Stream pdfStream);
PdfImageConverter.GetPageSizesFromBase64(string pdfBase64);
```

`PdfPageSize.Width` and `PdfPageSize.Height` are in PDF points.

### Large PDF Inputs

For large documents, prefer path-based APIs or seekable streams:

```csharp
using var stream = File.OpenRead("large.pdf");
int pageCount = PdfImageConverter.GetPageCount(stream, leaveOpen: true);
```

Seekable streams are loaded through PDFium custom file access, so the full PDF is not copied into a managed byte array. Byte array and Base64 APIs keep the full PDF in memory. Non-seekable streams are buffered into memory before loading because PDFium requires random access.

## Rendering

Zero-based page indexes:

```csharp
PdfBitmap bitmap = PdfImageConverter.RenderPage("input.pdf", pageIndex: 0);
```

One-based page numbers:

```csharp
PdfBitmap bitmap = PdfImageConverter.RenderPageNumber("input.pdf", pageNumber: 1);
```

Supported inputs:

```csharp
PdfImageConverter.RenderPage(string pdfPath, int pageIndex);
PdfImageConverter.RenderPage(byte[] pdfBytes, int pageIndex);
PdfImageConverter.RenderPage(Stream pdfStream, int pageIndex);
PdfImageConverter.RenderPageFromBase64(string pdfBase64, int pageIndex);

PdfImageConverter.RenderPageNumber(string pdfPath, int pageNumber);
PdfImageConverter.RenderPageNumber(byte[] pdfBytes, int pageNumber);
```

Render selected zero-based pages:

```csharp
IEnumerable<PdfBitmap> pages = PdfImageConverter.RenderPages("input.pdf", new[] { 0, 2, 4 });
```

Render all pages:

```csharp
IEnumerable<PdfBitmap> pages = PdfImageConverter.RenderDocument("input.pdf");
```

## Saving Images

Save with an explicit format:

```csharp
PdfImageConverter.SavePageNumber("input.pdf", pageNumber: 1, "page.bmp", new PdfImageConversionOptions
{
    Format = PdfImageOutputFormat.Bmp,
});
```

Convenience methods:

```csharp
PdfImageConverter.SavePng("input.pdf", pageNumber: 1, "page.png");
PdfImageConverter.SaveJpeg("input.pdf", pageNumber: 1, "page.jpg");
PdfImageConverter.SaveWebp("input.pdf", pageNumber: 1, "page.webp");
```

Save every page:

```csharp
int pageCount = PdfImageConverter.SaveDocument("input.pdf", "images", fileNamePrefix: "page");
```

Save selected zero-based page indexes or one-based page numbers while opening the PDF once:

```csharp
PdfImageConverter.SavePages("input.pdf", new[] { 0, 2 }, "images");
PdfImageConverter.SavePageNumbers("input.pdf", new[] { 1, 3 }, "images");
```

Generated names use 1-based numbering:

```text
page-0001.bmp
page-0002.bmp
page-0003.bmp
```

## Output Formats

```csharp
public enum PdfImageOutputFormat
{
    Bmp,
    Png,
    Jpeg,
    Webp,
}
```

BMP is written directly by PdfiumRaster. PNG, JPEG, and WebP are encoded with SkiaSharp.

## Conversion Options

```csharp
var options = new PdfImageConversionOptions
{
    Format = PdfImageOutputFormat.Png,
    ColorMode = PdfImageColorMode.BlackAndWhite,
    BlackAndWhiteThreshold = 160,
    Render = new PdfPageRenderOptions
    {
        Dpi = 300,
        Width = 1600,
        WithAspectRatio = true,
        Rotation = PdfPageRotation.Normal,
        Flags = PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
        AntiAliasing = PdfAntiAliasing.All,
        BackgroundColor = 0xFFFFFFFF,
    },
};
```

### Color Modes

```csharp
public enum PdfImageColorMode
{
    Color,
    Grayscale,
    BlackAndWhite,
}
```

`BlackAndWhiteThreshold` is applied to luminance after rendering. Lower values produce more white pixels; higher values produce more black pixels.

### Anti-Aliasing

```csharp
public enum PdfAntiAliasing
{
    None,
    Text,
    Images,
    Paths,
    All,
}
```

Use bitwise combinations when needed:

```csharp
AntiAliasing = PdfAntiAliasing.Text | PdfAntiAliasing.Paths
```

## Manual Bitmap Rendering

Use `PdfDocument` and `PdfPage` when you need placement control.

```csharp
using var pdfium = PdfiumLibrary.Initialize();
using var document = PdfDocument.Load("input.pdf");
using var page = document.LoadPage(0);

var target = PdfBitmap.Create(1200, 1600);

page.Render(
    target,
    startX: 20,
    startY: 20,
    sizeX: 1160,
    sizeY: 1560,
    rotate: PdfPageRotation.Normal,
    flags: PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
    backgroundColor: 0xFFFFFFFF);
```

When rendering the same page size repeatedly, render options can also be used with an existing bitmap:

```csharp
var options = new PdfPageRenderOptions { Dpi = 144 };
var (width, height) = options.GetPixelSize(page.Width, page.Height);
var reusable = PdfBitmap.Create(width, height);

page.Render(reusable, options);
```

## Writing Existing Bitmaps

```csharp
PdfImageWriter.SaveBmp(bitmap, "page.bmp");
PdfImageWriter.SavePng(bitmap, "page.png");
PdfImageWriter.SaveJpeg(bitmap, "page.jpg");
PdfImageWriter.SaveWebp(bitmap, "page.webp");
```

Stream writers are also available:

```csharp
PdfImageWriter.WriteBmp(bitmap, stream);
PdfImageWriter.WritePng(bitmap, stream);
PdfImageWriter.WriteJpeg(bitmap, stream);
PdfImageWriter.WriteWebp(bitmap, stream);
```

The converter facade also supports saving directly to streams:

```csharp
using var stream = File.Create("page.png");
PdfImageConverter.SavePng("input.pdf", pageNumber: 1, stream);
```

## Native Dependencies

Native PDFium binaries are provided through:

```text
bblanchon.PDFium.Linux
bblanchon.PDFium.macOS
bblanchon.PDFium.Win32
```

These are referenced with `PrivateAssets="analyzers"` so native runtime assets flow transitively to consuming applications.

## Threading

PDFium is not thread-safe. PdfiumRaster serializes calls into PDFium with a shared lock. Use separate processes for true parallel rendering.

## Current Scope

Supported:

- Desktop/server .NET via `netstandard2.0`
- PDF-to-image rendering
- PDFium native assets through NuGet
- BMP/PNG/JPEG/WebP output
- File, stream, byte array, and Base64 inputs

Not currently implemented:

- Async `IAsyncEnumerable` conversion
- Form-fill rendering
- Bounds/tiling rendering
- Browser/mobile-specific target frameworks
- .NET Framework-specific native loader customization
