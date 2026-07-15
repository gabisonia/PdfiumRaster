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

For faster encoding, opt into the speed preset. PNG compression level 1 may produce a larger file; JPEG and WebP
quality 85 are lossy:

```csharp
var fast = new PdfImageConversionOptions
{
    Format = PdfImageOutputFormat.Png,
    Encoding = PdfImageEncodingOptions.Fast,
};

PdfImageConverter.SavePageNumber("input.pdf", pageNumber: 1, "page-fast.png", fast);
```

Expected output:

```text
page-0001.png
page-0001.jpg
page-0001.webp
page-fast.png
```

## Load From Memory

Render from bytes, streams, or Base64 when the PDF does not live on disk.

```csharp
using PdfiumRaster;

var bytes = File.ReadAllBytes("input.pdf");
var bitmap = PdfImageConverter.RenderPageNumber(bytes, pageNumber: 1);

PdfImageWriter.SavePng(bitmap, "page-0001.png");

using var stream = File.OpenRead("input.pdf");
var fromStream = PdfImageConverter.RenderPage(stream, pageIndex: 0, leaveOpen: true);
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

var pageCount = PdfImageConverter.SaveDocument("input.pdf", "images", fileNamePrefix: "page", options: new PdfImageConversionOptions
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

Use this when you need explicit placement inside a destination bitmap.

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

## Repeated Low-Allocation Rendering

Use `PdfRenderSession` when rendering or encoding multiple pages from one PDF. It caches the current page and reusable
render buffer. Page indexes are zero-based.

```csharp
using PdfiumRaster;

using var session = PdfRenderSession.Open("input.pdf");
var options = new PdfImageConversionOptions
{
    Render = PdfPageRenderOptions.ScreenPreview,
    Format = PdfImageOutputFormat.Png,
    Encoding = PdfImageEncodingOptions.Fast,
};

Directory.CreateDirectory("images");

for (var pageIndex = 0; pageIndex < session.PageCount; pageIndex++)
{
    session.SavePage(pageIndex, $"images/page-{pageIndex + 1:D4}.png", options);
}
```

For synchronous pixel processing without allocating an output bitmap on every render, use the callback overload:

```csharp
byte firstBlue = session.RenderPage(
    pageIndex: 0,
    callback: bitmap => bitmap.Pixels[0],
    options: options);
```

The callback bitmap is owned by the session and is valid only until the callback returns. Do not store the bitmap or
its `Pixels` array. A session permits only one operation at a time.

## Write To A Caller-Owned Stream

Output stream overloads leave the stream open, which is useful for HTTP responses and object-storage uploads:

```csharp
using var output = new MemoryStream();

PdfImageConverter.SavePng(
    "input.pdf",
    pageNumber: 1,
    imageStream: output,
    options: new PdfImageConversionOptions
    {
        Render = PdfPageRenderOptions.ScreenPreview,
        Encoding = PdfImageEncodingOptions.Fast,
    });

output.Position = 0;
```

## Rendering Test Output

The normal test suite renders tracked PDF assets and writes generated images under the test output directory:

```text
tests/PdfiumRaster.Tests/bin/Debug/net10.0/TestOutput/
```

Run the normal test suite:

```bash
make test
```

Run local-only tests that use ignored assets such as `tests/PdfiumRaster.Tests/TestAssets/annotations.pdf`:

```bash
make test-local
```
