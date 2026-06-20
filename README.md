# PdfiumRaster

[![NuGet version](https://img.shields.io/nuget/vpre/PdfiumRaster.svg)](https://www.nuget.org/packages/PdfiumRaster)
[![CI](https://github.com/gabisonia/PdfiumRaster/actions/workflows/ci.yml/badge.svg)](https://github.com/gabisonia/PdfiumRaster/actions/workflows/ci.yml)
[![Publish NuGet](https://github.com/gabisonia/PdfiumRaster/actions/workflows/publish-nuget.yml/badge.svg)](https://github.com/gabisonia/PdfiumRaster/actions/workflows/publish-nuget.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

PdfiumRaster is a .NET Standard PDF-to-image library backed by PDFium. It focuses on rendering PDF pages to bitmap images with practical controls for DPI, page number, annotations, rotation, sizing, anti-aliasing, grayscale, and black-and-white output.

## Features

- Render one page or every page in a document
- Use zero-based page indexes or 1-based page numbers
- Load PDFs from file paths, streams, byte arrays, or Base64 strings
- Export BMP, PNG, JPEG, and WebP
- Query page count and page sizes
- Render annotations
- Render grayscale or thresholded black-and-white images
- Configure DPI, width, height, aspect ratio, rotation, background color, and anti-aliasing
- Native PDFium binaries delivered through NuGet runtime assets

## Install Model

The library targets `netstandard2.0`.

Native PDFium binaries are delivered through the same NuGet asset pattern used by PDFtoImage:

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
var fromStream = PdfImageConverter.RenderPage(stream, pageIndex: 0);

var base64 = Convert.ToBase64String(bytes);
var fromBase64 = PdfImageConverter.RenderPageFromBase64(base64, pageIndex: 0);
```

For large PDFs, prefer a file path or a seekable stream such as `FileStream`. Seekable streams are passed to PDFium through random-access callbacks and are not copied into one managed byte array. Byte arrays and Base64 strings necessarily keep the full PDF in memory. Non-seekable streams are buffered into memory before loading because PDFium needs random access.

## Page Metadata

```csharp
var pageCount = PdfImageConverter.GetPageCount("sample.pdf");
var pageSizes = PdfImageConverter.GetPageSizes("sample.pdf");
```

`PdfPageSize.Width` and `PdfPageSize.Height` are PDF points.

## Manual Rendering

Use the lower-level API when you need placement control similar to Patagames-style rendering.

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

## Threading

PDFium is not thread-safe. PdfiumRaster serializes native PDFium calls with a shared lock. Process one PDF at a time inside a process; use multiple processes if you need true parallel PDF rendering.

## Repository Layout

```text
src/PdfiumRaster/                  library source
tests/PdfiumRaster.Tests/          unit and rendering tests
tests/PdfiumRaster.Tests/TestAssets/annotations.pdf
samples/                           sample descriptions
docs/                              API and behavior notes
```

## Tests

Run all tests:

```bash
dotnet test PdfiumRaster.slnx
```

The annotation export test writes generated images to:

```text
tests/PdfiumRaster.Tests/bin/Debug/net10.0/TestOutput/annotations/
```

## Samples

See [samples/README.md](samples/README.md) for additional sample scenarios and expected outputs.

## Documentation

- [API reference](docs/API.md)
- [Sample descriptions](samples/README.md)
- [Release checklist](docs/RELEASING.md)

## License

PdfiumRaster is licensed under the [MIT License](LICENSE).
