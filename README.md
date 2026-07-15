# PdfiumRaster

[![NuGet version](https://img.shields.io/nuget/vpre/PdfiumRaster.svg)](https://www.nuget.org/packages/PdfiumRaster)
[![CI](https://github.com/gabisonia/PdfiumRaster/actions/workflows/ci.yml/badge.svg)](https://github.com/gabisonia/PdfiumRaster/actions/workflows/ci.yml)
[![Publish NuGet](https://github.com/gabisonia/PdfiumRaster/actions/workflows/publish-nuget.yml/badge.svg)](https://github.com/gabisonia/PdfiumRaster/actions/workflows/publish-nuget.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

PdfiumRaster is a .NET Standard PDF-to-image library backed by PDFium. It focuses on rendering PDF pages to bitmap images with practical controls for DPI, page number, annotations, rotation, sizing, anti-aliasing, grayscale, and black-and-white output.

"Raster" means the library converts PDF pages into pixel-based images. PDFs can contain vector text, paths, annotations, and embedded images; PdfiumRaster uses PDFium to draw that page content into a bitmap and then writes it as formats such as BMP, PNG, JPEG, or WebP.

## Features

- Render one page or every page in a document
- Use zero-based page indexes or 1-based page numbers
- Load PDFs from file paths, streams, byte arrays, or Base64 strings
- Export BMP, PNG, JPEG, and WebP
- Query page count and page sizes
- Render annotations
- Render grayscale or thresholded black-and-white images
- Configure DPI, width, height, aspect ratio, rotation, background color, and anti-aliasing
- Reuse an open document, loaded page, and render buffer with `PdfRenderSession`
- Select explicit screen-preview and fast-encoding presets without changing quality-oriented defaults
- Native PDFium binaries delivered through NuGet runtime assets

## Installation

Install the package from NuGet:

```bash
dotnet add package PdfiumRaster
```

The library targets `netstandard2.0` and is intended for desktop and server applications on Windows, Linux, and
macOS. The consuming application must run on a platform and architecture covered by the PDFium and SkiaSharp runtime
assets restored by NuGet.

Native PDFium binaries are delivered through platform-specific NuGet runtime packages:

```xml
bblanchon.PDFium.Linux
bblanchon.PDFium.macOS
bblanchon.PDFium.Win32
```

SkiaSharp is used for PNG, JPEG, and WebP encoding. BMP output is written directly by the library.

When a consuming app restores/builds, native assets are copied under RID-specific folders such as:

```text
runtimes/
  win-x64/native/pdfium.dll
  linux-x64/native/libpdfium.so
  osx-arm64/native/libpdfium.dylib
```

## Quick Start

Render the first page as PNG using a 1-based page number:

```csharp
using PdfiumRaster;

PdfImageConverter.SavePng("sample.pdf", pageNumber: 1, "page-0001.png");
```

Render all pages as BMP files:

```csharp
using PdfiumRaster;

var pageCount = PdfImageConverter.SaveDocument("sample.pdf", "images", options: new PdfImageConversionOptions
{
    Format = PdfImageOutputFormat.Bmp,
    Render = new PdfPageRenderOptions
    {
        Dpi = 300,
        Flags = PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
    },
});
```

Output:

```text
images/page-0001.bmp
images/page-0002.bmp
images/page-0003.bmp
```

Render selected pages while opening the PDF once:

```csharp
PdfImageConverter.SavePageNumbers("sample.pdf", new[] { 1, 3 }, "images");
```

For repeated latency-sensitive rendering or batch encoding, use a session. It keeps the document open and reuses the
loaded page and render buffer when possible:

```csharp
using var session = PdfRenderSession.Open("sample.pdf");
var options = new PdfImageConversionOptions
{
    Render = PdfPageRenderOptions.ScreenPreview,
    Format = PdfImageOutputFormat.Png,
    Encoding = PdfImageEncodingOptions.Fast,
};

for (var pageIndex = 0; pageIndex < session.PageCount; pageIndex++)
{
    session.SavePage(pageIndex, $"page-{pageIndex + 1:D4}.png", options);
}
```

For synchronous in-memory processing with no output bitmap allocation, use the session callback overload. The bitmap
is session-owned and must not be retained after the callback returns:

```csharp
byte firstBlue = session.RenderPage(
    pageIndex: 0,
    callback: bitmap => bitmap.Pixels[0],
    options: options);
```

## Page Numbers

`SavePage` and `RenderPage` use zero-based page indexes. `SavePageNumber` and `RenderPageNumber` use human-friendly 1-based page numbers.

```csharp
PdfImageConverter.SavePage("sample.pdf", pageIndex: 0, "zero-based.bmp");
PdfImageConverter.SavePageNumber("sample.pdf", pageNumber: 1, "one-based.bmp");
```

## Output Formats

```csharp
PdfImageConverter.SavePageNumber("sample.pdf", 1, "page.bmp", new PdfImageConversionOptions
{
    Format = PdfImageOutputFormat.Bmp,
});

PdfImageConverter.SavePng("sample.pdf", 1, "page.png");
PdfImageConverter.SaveJpeg("sample.pdf", 1, "page.jpg");
PdfImageConverter.SaveWebp("sample.pdf", 1, "page.webp");
```

PNG defaults to Skia's compression setting, while JPEG and WebP default to quality 100. To favor throughput, select
encoding settings explicitly:

```csharp
var options = new PdfImageConversionOptions
{
    Format = PdfImageOutputFormat.Png,
    Encoding = PdfImageEncodingOptions.Fast, // PNG level 1; JPEG/WebP quality 85
};
```

The fast preset prioritizes encoding speed. PNG level 1 can produce larger files than higher compression levels;
JPEG and WebP quality 85 normally reduce output size at the cost of lossy compression.

## Render Options

```csharp
PdfImageConverter.SavePng("sample.pdf", pageNumber: 1, "page.png", new PdfImageConversionOptions
{
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
});
```

## Grayscale And Black-And-White

```csharp
PdfImageConverter.SavePageNumber("sample.pdf", pageNumber: 1, "page-gray.png", new PdfImageConversionOptions
{
    Format = PdfImageOutputFormat.Png,
    ColorMode = PdfImageColorMode.Grayscale,
});

PdfImageConverter.SavePageNumber("sample.pdf", pageNumber: 1, "page-bw.png", new PdfImageConversionOptions
{
    Format = PdfImageOutputFormat.Png,
    ColorMode = PdfImageColorMode.BlackAndWhite,
    BlackAndWhiteThreshold = 160,
});
```

## Load From Memory Or Stream

```csharp
var bytes = File.ReadAllBytes("sample.pdf");
var bitmap = PdfImageConverter.RenderPageNumber(bytes, pageNumber: 1);

using var stream = File.OpenRead("sample.pdf");
var fromStream = PdfImageConverter.RenderPage(stream, pageIndex: 0, leaveOpen: true);

var base64 = Convert.ToBase64String(bytes);
var fromBase64 = PdfImageConverter.RenderPageFromBase64(base64, pageIndex: 0);
```

For large PDFs, prefer a file path or a seekable stream such as `FileStream`. Seekable streams are passed to PDFium
through random-access callbacks and are not copied into one managed byte array. Byte arrays and Base64 strings
necessarily keep the full PDF in memory. Non-seekable streams are buffered into memory before loading because PDFium
needs random access. Input stream overloads dispose the stream by default; pass `leaveOpen: true` when the caller owns
the stream.

## Production Safety

Treat PDFs as untrusted native-parser input. Keep PdfiumRaster and its PDFium runtime packages current, and enforce
application-level limits for input bytes, page count, render DPI or dimensions, total output pixels, and request time.
Do not pass unbounded user-controlled DPI, width, height, or scale values directly to rendering APIs. For stronger
fault and resource isolation, render untrusted documents in a separately supervised process.

## Page Metadata

```csharp
var pageCount = PdfImageConverter.GetPageCount("sample.pdf");
var pageSizes = PdfImageConverter.GetPageSizes("sample.pdf");
```

`PdfPageSize.Width` and `PdfPageSize.Height` are PDF points.

## Manual Rendering

Use the lower-level API when you need explicit document/page lifetimes or placement inside a destination bitmap.

```csharp
using PdfiumRaster;

using var pdfium = PdfiumLibrary.Initialize();
using var document = PdfDocument.Load("sample.pdf");
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

PdfImageWriter.SavePng(target, "placed-page.png");
```

For repeated same-size rendering, rent a pooled bitmap and dispose the lease when finished:

```csharp
var options = new PdfPageRenderOptions { Dpi = 144 };
var (width, height) = options.GetPixelSize(page.Width, page.Height);

using var lease = PdfBitmapLease.Rent(width, height, clear: false);
page.Render(lease, options);

PdfImageWriter.SavePng(lease.Bitmap, "page.png");
```

The high-level facade can also render a file path into an existing bitmap:

```csharp
PdfImageConverter.RenderPageInto("sample.pdf", pageIndex: 0, lease.Bitmap, new PdfImageConversionOptions
{
    Render = options,
});
```

Do not keep `lease.Bitmap` or `lease.Bitmap.Pixels` after disposing the lease. Save helpers reuse pooled buffers internally where possible.

When rendering several pages individually, keep the PDF open and use the document-scoped overloads:

```csharp
using var pdfium = PdfiumLibrary.Initialize();
using var document = PdfDocument.Load("sample.pdf");
var options = new PdfPageRenderOptions { Dpi = 96 };

for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
{
    var bitmap = PdfImageConverter.RenderPage(document, pageIndex, options);
    PdfImageWriter.SaveJpeg(bitmap, $"page-{pageIndex + 1:D4}.jpg", quality: 85);
}
```

Rendering time and memory grow with pixel count. The default is 300 DPI for high-resolution output; use
`PdfPageRenderOptions.ScreenPreview` for a fresh 96 DPI preset when that resolution is sufficient.

## Threading

PDFium is not thread-safe. PdfiumRaster serializes native PDFium calls with a process-wide shared lock, so concurrent
render requests in one process do not execute inside PDFium in parallel. A `PdfRenderSession` also permits only one
operation at a time and rejects concurrent or reentrant use. Use separate processes when true parallel PDF rendering
is required.

## Repository Layout

```text
src/PdfiumRaster/                  library source
tests/PdfiumRaster.Tests/          unit and rendering tests
tests/PdfiumRaster.Tests/TestAssets/ tracked and local-only test PDFs
samples/                           sample descriptions
docs/                              API and behavior notes
```

## Tests

Run the normal test suite:

```bash
make test
```

Run local-only tests that use ignored assets such as `annotations.pdf`:

```bash
make test-local
```

Rendering tests write generated images under the test output directory. Normal tests use tracked PDFs, while `make test-local` also writes local annotation renders from ignored `annotations.pdf`:

```text
tests/PdfiumRaster.Tests/bin/Debug/net10.0/TestOutput/
tests/PdfiumRaster.Tests/bin/Debug/net10.0/TestOutput/annotations/
tests/PdfiumRaster.Tests/bin/Debug/net10.0/TestOutput/formats/
```

## Samples

See [samples/README.md](samples/README.md) for additional sample scenarios and expected outputs.

## Documentation

- [API reference](docs/API.md)
- [Architecture and technical design](docs/ARCHITECTURE.md)
- [Performance notes](docs/PERFORMANCE.md)
- [Sample descriptions](samples/README.md)
- [Release checklist](docs/RELEASING.md)

## License

PdfiumRaster is licensed under the [MIT License](LICENSE).
