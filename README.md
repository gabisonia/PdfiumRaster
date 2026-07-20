# PdfiumRaster

[![NuGet](https://img.shields.io/nuget/v/PdfiumRaster.svg)](https://www.nuget.org/packages/PdfiumRaster)
[![CI](https://github.com/gabisonia/PdfiumRaster/actions/workflows/ci.yml/badge.svg)](https://github.com/gabisonia/PdfiumRaster/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/gabisonia/PdfiumRaster/blob/master/LICENSE)

PdfiumRaster is a .NET Standard library for rendering PDF pages to BMP, PNG, JPEG, or WebP images. PDFium handles
page rendering; SkiaSharp handles compressed image encoding.

> [!IMPORTANT]
> This project is intentionally limited to PDF-to-image conversion. It does not provide PDF editing, text extraction,
> form filling, signing, or a viewer UI.

## Features

- Render one page, selected pages, or a complete document
- Load PDFs from paths, streams, byte arrays, or Base64 strings
- Set DPI, output dimensions, rotation, background, annotations, and anti-aliasing
- Produce color, grayscale, or thresholded black-and-white output
- Query page count and page dimensions
- Reuse document and bitmap resources for repeated rendering
- Save directly from PDFium-owned pixel memory without a full-page managed buffer
- Queue unrelated render requests with bounded backpressure
- Restore native PDFium and SkiaSharp runtime assets through NuGet

## PdfiumRaster And PDFiumCore

[PDFiumCore](https://github.com/Dtronix/PDFiumCore) provides low-level .NET bindings for direct access to the PDFium
C API. PdfiumRaster operates at a higher level and intentionally focuses on the complete PDF-to-image workflow.

| Concern | PdfiumRaster | PDFiumCore |
| --- | --- | --- |
| API level | Document, page, conversion, session, and dispatcher APIs | Direct bindings that closely follow PDFium functions and handles |
| Image output | Writes BMP, PNG, JPEG, and WebP | Exposes PDFium bitmap and rendering primitives; the application chooses how to encode or consume pixels |
| Resource handling | Manages PDFium initialization, document/page lifetimes, stream callbacks, buffer reuse, and native-call serialization | Gives the application low-level control and responsibility for composing those concerns |
| Scope | Deliberately limited to rendering PDF pages as images | Suitable when direct access to the broader PDFium API is required |

Choose PdfiumRaster when the goal is reliable PDF-to-image conversion without building an interop and encoding layer.
Choose PDFiumCore when the application needs lower-level PDFium features or precise control beyond PdfiumRaster's
focused rendering API. The projects serve different layers rather than being drop-in replacements for each other.

## Requirements

The library targets `netstandard2.1`. The application using it must run on a Windows, Linux, or macOS runtime
identifier supported by the PDFium and SkiaSharp packages in the dependency graph.

## Installation

```bash
dotnet add package PdfiumRaster
```

No manual PDFium copy step is required for supported runtime identifiers.

PdfiumRaster 2.0 and later require a runtime that implements .NET Standard 2.1. .NET Framework and other
`netstandard2.0`-only consumers should remain on PdfiumRaster 1.0.x.

## Quick Start

Render the first page to PNG. Page-number methods use 1-based numbers:

```csharp
using PdfiumRaster;

PdfImageConverter.SavePng("input.pdf", pageNumber: 1, "page-0001.png");
```

The default render resolution is 300 DPI. For previews, select the 96-DPI preset explicitly:

```csharp
PdfImageConverter.SavePng(
    "input.pdf",
    pageNumber: 1,
    "preview.png",
    new PdfImageConversionOptions
    {
        Render = PdfPageRenderOptions.ScreenPreview,
    });
```

Export every page as PNG:

```csharp
int pageCount = PdfImageConverter.SaveDocument(
    "input.pdf",
    "images",
    options: new PdfImageConversionOptions
    {
        Format = PdfImageOutputFormat.Png,
        Render = new PdfPageRenderOptions { Dpi = 150 },
    });
```

Files are named with 1-based page numbers:

```text
images/page-0001.png
images/page-0002.png
```

## Page Indexes And Page Numbers

Methods named `RenderPage` or `SavePage` take a zero-based `pageIndex`. Methods named `RenderPageNumber` or
`SavePageNumber` take a 1-based `pageNumber`.

```csharp
PdfImageConverter.SavePage("input.pdf", pageIndex: 0, "index.bmp");
PdfImageConverter.SavePageNumber("input.pdf", pageNumber: 1, "number.bmp");
```

Batch methods follow the same convention:

```csharp
PdfImageConverter.SavePages("input.pdf", new[] { 0, 2 }, "images");
PdfImageConverter.SavePageNumbers("input.pdf", new[] { 1, 3 }, "images");
```

## Render And Encoding Options

```csharp
var options = new PdfImageConversionOptions
{
    Format = PdfImageOutputFormat.Jpeg,
    ColorMode = PdfImageColorMode.Grayscale,
    Encoding = new PdfImageEncodingOptions
    {
        Quality = 85,
    },
    Render = new PdfPageRenderOptions
    {
        Dpi = 150,
        Width = 1600,
        WithAspectRatio = true,
        Rotation = PdfPageRotation.Normal,
        Flags = PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
        AntiAliasing = PdfAntiAliasing.All,
        BackgroundColor = 0xFFFFFFFF,
    },
};

PdfImageConverter.SavePageNumber("input.pdf", pageNumber: 1, "page.jpg", options);
```

When `WithAspectRatio` is `true`, set either `Width` or `Height`, not both. Dimensions are pixels; PDF page dimensions
reported by `GetPageSizes` are PDF points (72 points per inch).

`PdfImageEncodingOptions.Fast` selects PNG compression level 1 and JPEG/WebP quality 85. It favors encoding speed;
the PNG may be larger, and JPEG/WebP output is lossy.

## Input And Memory Behavior

For large PDFs, use a file path or seekable stream. Seekable streams use PDFium random-access callbacks and are not
copied into one managed byte array.

```csharp
using var input = File.OpenRead("input.pdf");
PdfBitmap bitmap = PdfImageConverter.RenderPage(
    input,
    pageIndex: 0,
    leaveOpen: true);
```

- Input streams are disposed by default. Pass `leaveOpen: true` when the caller owns the stream.
- Output stream overloads leave the destination stream open.
- Non-seekable input streams are buffered once because PDFium requires random access; the backing buffer remains in
  managed memory for the open document lifetime.
- Byte-array and Base64 APIs keep the complete PDF in managed memory.
- Returned `PdfBitmap` pixels are BGRA and owned by the caller.
- Rendered bitmap memory grows with output width and height, regardless of the source PDF file size.

## Repeated Rendering

Use `PdfRenderSession` for several operations on the same PDF. It keeps the document open and reuses its current page
and render buffer where possible.

```csharp
using var session = PdfRenderSession.Open("input.pdf");
var options = new PdfImageConversionOptions
{
    Format = PdfImageOutputFormat.Png,
    Render = PdfPageRenderOptions.ScreenPreview,
    Encoding = PdfImageEncodingOptions.Fast,
};

Directory.CreateDirectory("images");

for (var pageIndex = 0; pageIndex < session.PageCount; pageIndex++)
{
    session.SavePage(pageIndex, $"images/page-{pageIndex + 1:D4}.png", options);
}
```

A session accepts one operation at a time. Its callback render overload exposes a session-owned bitmap that is valid
only for the duration of the callback; do not retain that bitmap or its pixel array.

## Concurrent Requests

Use one shared `PdfRenderDispatcher` when unrelated PDFs arrive concurrently. It provides a bounded asynchronous queue
and can encode completed images on multiple workers.

```csharp
using var dispatcher = new PdfRenderDispatcher(new PdfRenderDispatcherOptions
{
    QueueCapacity = 32,
    EncodingConcurrency = 2,
    QueueFullMode = PdfRenderQueueFullMode.Wait,
});

var options = new PdfImageConversionOptions
{
    Format = PdfImageOutputFormat.Png,
    Render = PdfPageRenderOptions.ScreenPreview,
};

await Task.WhenAll(
    dispatcher.SavePageAsync("first.pdf", pageIndex: 0, "first.png", options),
    dispatcher.SavePageAsync("second.pdf", pageIndex: 0, "second.png", options));

await dispatcher.CompleteAsync();
```

PDFium calls remain serialized process-wide; the dispatcher does not render two pages inside PDFium simultaneously.
It improves request backpressure and allows encoding/output to overlap. Use supervised worker processes if native
rendering must run in parallel or untrusted PDFs need stronger isolation.

## Operational Safety

PDFium is native code parsing potentially untrusted input. Keep PdfiumRaster and its dependencies current. Applications
that accept untrusted PDFs should limit input size, page count, output dimensions, DPI, total rendered pixels, and
execution time. Public argument validation does not impose application-specific resource limits.

## Documentation

- [API guide](https://github.com/gabisonia/PdfiumRaster/blob/master/docs/API.md)
- [Samples](https://github.com/gabisonia/PdfiumRaster/blob/master/samples/README.md)
- [Architecture](https://github.com/gabisonia/PdfiumRaster/blob/master/docs/ARCHITECTURE.md)
- [Performance and benchmarks](https://github.com/gabisonia/PdfiumRaster/blob/master/docs/PERFORMANCE.md)
- [Contributing](https://github.com/gabisonia/PdfiumRaster/blob/master/CONTRIBUTING.md)
- [Security policy](https://github.com/gabisonia/PdfiumRaster/blob/master/SECURITY.md)
- [Release process](https://github.com/gabisonia/PdfiumRaster/blob/master/docs/RELEASING.md)

## Development

The repository uses the root `Makefile` as its command surface:

```bash
make restore
make build
make test
make pack
```

See the [contribution guide](https://github.com/gabisonia/PdfiumRaster/blob/master/CONTRIBUTING.md) before opening a
pull request.

## License

PdfiumRaster is available under the
[MIT License](https://github.com/gabisonia/PdfiumRaster/blob/master/LICENSE).
