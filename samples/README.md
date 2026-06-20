# PdfiumRaster Samples

These samples describe the supported PDF-to-image workflows. They are intentionally small so each one maps directly to a public API.

## Export One Page

Render a single PDF page to a 32-bit BGRA BMP file.

```csharp
using PdfiumRaster;

PdfImageConverter.SavePage("input.pdf", pageIndex: 0, "page-0001.bmp", new PdfImageConversionOptions
{
    Render = new PdfPageRenderOptions
    {
        Dpi = 300,
        Flags = PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
    },
});
```

Expected output:

```text
page-0001.bmp
```

## Export By Page Number

Use the 1-based page-number API when the caller sees page numbers instead of zero-based indexes.

```csharp
using PdfiumRaster;

PdfImageConverter.SavePageNumber("input.pdf", pageNumber: 1, "page-0001.bmp", new PdfImageConversionOptions
{
    Render = new PdfPageRenderOptions
    {
        Dpi = 300,
        Flags = PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
    },
});
```

Expected output:

```text
page-0001.bmp
```

## Export Black And White

Render a page and threshold it to black and white. The threshold is applied to luminance after rendering.

```csharp
using PdfiumRaster;

PdfImageConverter.SavePageNumber("input.pdf", pageNumber: 1, "page-0001-bw.bmp", new PdfImageConversionOptions
{
    ColorMode = PdfImageColorMode.BlackAndWhite,
    BlackAndWhiteThreshold = 160,
    Render = new PdfPageRenderOptions
    {
        Dpi = 300,
        Flags = PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
    },
});
```

Expected output:

```text
page-0001-bw.bmp
```

## Export Encoded Formats

Use SkiaSharp-backed encoders for common image formats.

```csharp
using PdfiumRaster;

PdfImageConverter.SavePng("input.pdf", pageNumber: 1, "page-0001.png");
PdfImageConverter.SaveJpeg("input.pdf", pageNumber: 1, "page-0001.jpg");
PdfImageConverter.SaveWebp("input.pdf", pageNumber: 1, "page-0001.webp");
```

Expected output:

```text
page-0001.png
page-0001.jpg
page-0001.webp
```

## Load From Memory

Render from bytes, streams, or Base64 when the PDF does not live on disk.

```csharp
using PdfiumRaster;

var bytes = File.ReadAllBytes("input.pdf");
var bitmap = PdfImageConverter.RenderPageNumber(bytes, pageNumber: 1);

PdfImageWriter.SavePng(bitmap, "page-0001.png");

using var stream = File.OpenRead("input.pdf");
var fromStream = PdfImageConverter.RenderPage(stream, pageIndex: 0);
PdfImageWriter.SavePng(fromStream, "page-0001-stream.png");

var base64 = Convert.ToBase64String(bytes);
var fromBase64 = PdfImageConverter.RenderPageFromBase64(base64, pageIndex: 0);
PdfImageWriter.SavePng(fromBase64, "page-0001-base64.png");
```

## Query Page Metadata

```csharp
using PdfiumRaster;

var pageCount = PdfImageConverter.GetPageCount("input.pdf");
var pageSizes = PdfImageConverter.GetPageSizes("input.pdf");
```

`PdfPageSize.Width` and `PdfPageSize.Height` are in PDF points.

## Custom Size And Anti-Aliasing

```csharp
using PdfiumRaster;

PdfImageConverter.SavePng("input.pdf", pageNumber: 1, "page-0001.png", new PdfImageConversionOptions
{
    Render = new PdfPageRenderOptions
    {
        Width = 1600,
        WithAspectRatio = true,
        AntiAliasing = PdfAntiAliasing.Text | PdfAntiAliasing.Paths,
        Flags = PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
    },
});
```

## Export All Pages

Render every page in a PDF into a directory with stable numbered file names.

```csharp
using PdfiumRaster;

var pageCount = PdfImageConverter.SaveDocument("input.pdf", "images", fileNamePrefix: "page", new PdfImageConversionOptions
{
    Render = new PdfPageRenderOptions
    {
        Dpi = 300,
        Flags = PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
    },
});
```

Expected output:

```text
images/page-0001.bmp
images/page-0002.bmp
images/page-0003.bmp
```

## Render Into A Custom Bitmap

Use this when you need placement control similar to Patagames-style rendering.

```csharp
using PdfiumRaster;

using var pdfium = PdfiumLibrary.Initialize();
using var document = PdfDocument.Load("input.pdf");
using var page = document.LoadPage(0);

var bitmap = PdfBitmap.Create(1200, 1600);

page.Render(
    bitmap,
    startX: 20,
    startY: 20,
    sizeX: 1160,
    sizeY: 1560,
    rotate: PdfPageRotation.Normal,
    flags: PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
    backgroundColor: 0xFFFFFFFF);

PdfImageWriter.SaveBmp(bitmap, "placed-page.bmp");
```

Expected output:

```text
placed-page.bmp
```

## Annotation Test Output

The test suite uses `tests/PdfiumRaster.Tests/TestAssets/annotations.pdf` and writes rendered pages to:

```text
tests/PdfiumRaster.Tests/bin/Debug/net10.0/TestOutput/annotations/
```

Run only that export test:

```bash
dotnet test PdfiumRaster.slnx --filter Export_all_annotations_pdf_pages_to_test_output
```
