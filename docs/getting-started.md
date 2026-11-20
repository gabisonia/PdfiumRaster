# Getting started

PdfiumRaster targets .NET Standard 2.1. Run it on Windows, Linux, or macOS with a runtime identifier supported by its PDFium and SkiaSharp dependencies. Supported native assets are restored through NuGet.

```bash
dotnet add package PdfiumRaster
```

Render the first page to PNG:

```csharp
using PdfiumRaster;

PdfImageConverter.SavePng("input.pdf", pageNumber: 1, "page-0001.png");
```

The default resolution is 300 DPI. For previews, use `PdfPageRenderOptions.ScreenPreview` (96 DPI), as shown in the [examples](examples.md).

## Page indexes and numbers

`RenderPage` and `SavePage` use zero-based `pageIndex` values; `RenderPageNumber` and `SavePageNumber` use one-based `pageNumber` values. Format helpers such as `SavePng` use one-based page numbers. For example, both calls select the first page:

```csharp
PdfImageConverter.SavePage("input.pdf", pageIndex: 0, "index.bmp");
PdfImageConverter.SavePageNumber("input.pdf", pageNumber: 1, "number.bmp");
```

The same distinction applies to `SavePages` and `SavePageNumbers`. Exported filenames use one-based page numbers.

## Inputs and memory

For large PDFs, prefer a file path or seekable stream. Seekable streams use PDFium random-access callbacks without copying the complete PDF into a managed byte array. Non-seekable streams are buffered for the open document lifetime; byte-array and Base64 inputs keep the complete PDF in managed memory. Rendered bitmap memory also grows with pixel dimensions and DPI.

Input streams are disposed by default; pass `leaveOpen: true` to retain ownership. Output stream overloads leave the destination open. Returned `PdfBitmap` objects and their BGRA pixel buffers belong to the caller. Session callback bitmaps are session-owned and valid only until the callback returns. See the [API guide](API.md) for details.
