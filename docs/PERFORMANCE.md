# PdfiumRaster Performance

This document tracks how PdfiumRaster measures and improves rendering performance and memory use.

## Goals

The primary optimization target is lower memory use during PDF-to-image conversion. Speed improvements are useful, but changes should not increase peak memory substantially or weaken native lifetime safety.

## Benchmarking

Run benchmarks with:

```bash
make benchmark
```

BenchmarkDotNet output is written to:

```text
BenchmarkDotNet.Artifacts/
```

Benchmarks cover:

- Rendering one page to `PdfBitmap`
- Repeated single-page rendering
- Saving one page to PNG
- Saving one page to JPEG
- Saving all pages as BMP
- Saving all pages as PNG and JPEG
- Saving selected pages
- Applying color, grayscale, and black-and-white color conversion
- An equivalent hot-render comparison with PDFiumCore

Use Release builds and compare allocated bytes as well as mean runtime. For image output benchmarks, remember that `MemoryStream.ToArray()` allocates the final encoded byte array so the result can be consumed by BenchmarkDotNet.

## PDFiumCore Comparison

Run only the wrapper comparison with:

```bash
make benchmark-compare
```

The comparison intentionally uses PDFiumCore `151.0.7891`, matching PdfiumRaster's PDFium native packages. Both paths:

- Use the same loaded native PDFium library in each benchmark process
- Keep the document and page open
- Render at the same 144 DPI pixel dimensions
- Render annotations and LCD text onto a white background
- Render directly into pinned, reusable managed BGRA buffers
- Read one output byte so the rendered result remains observable
- Exclude image encoding and file output

On an Apple M3 Pro with .NET 10, the initial comparison measured 1.556 ms for PdfiumRaster and 1.573 ms for
PDFiumCore, with zero managed allocation per render. The ratio was 1.01 and the confidence intervals overlapped, so
the two wrappers were effectively tied under equivalent conditions. Treat these numbers as a local baseline and rerun
the benchmark on every target platform and representative PDF corpus.

## Current Memory Optimizations

PNG, JPEG, and WebP encoding pins the existing `PdfBitmap.Pixels` buffer and passes it directly to SkiaSharp during synchronous encoding. This avoids allocating and copying a second full-size managed pixel buffer before encoding.

BMP output writes rows directly from the existing `PdfBitmap.Pixels` buffer.

`SaveDocument` opens the PDF once, caches the page count, and processes pages one at a time. This avoids retaining rendered bitmaps for the entire document export.

`SavePage`, `SavePageNumber`, `SaveDocument`, `SavePages`, and `SavePageNumbers` render through pooled page bitmaps when writing directly to files or streams. Prefer save helpers when the rendered bitmap does not need to be returned to the caller.

Grayscale conversion uses PDFium grayscale rendering and skips a managed post-processing pass. Black-and-white conversion also renders through PDFium grayscale, then applies a threshold pass using the grayscale channel instead of recalculating RGB luminance for every pixel.

`PdfPage.Render(PdfBitmap, PdfPageRenderOptions)` and path-based `PdfImageConverter.RenderPageInto` overloads let callers reuse a destination bitmap when the output dimensions stay stable. `PdfBitmapLease` can rent that destination buffer from `ArrayPool<byte>`; dispose the lease as soon as the bitmap is no longer needed, and do not retain its pixel array afterward.

`PdfPage.Render(PdfBitmapLease, PdfPageRenderOptions)` additionally keeps the leased pixels pinned and reuses one
native PDFium bitmap for the lease lifetime. Direct save helpers use this path internally. When repeatedly calling the
converter facade, use the `PdfDocument` overloads to keep the document open rather than paying initialization and file
parsing costs for every page.

`PdfDocument.PageCount` and loaded page dimensions are cached for the native document and page lifetimes. Background
fill and page rendering are also issued within one serialized native operation.

## Usage Guidance

Prefer file path or seekable stream APIs for large PDFs. Byte array and Base64 APIs keep the entire PDF in managed memory.

Use `SaveDocument` for full-document export instead of repeatedly calling single-page helpers. Single-page save helpers are optimized for memory, but still reopen the document each time.

Use `SavePages` or `SavePageNumbers` when exporting only part of a document.

Output dimensions are the primary rendering cost. The default 300 DPI is intended for high-resolution output. Use an
explicit 72 or 96 DPI for thumbnails and previews, or set an explicit pixel width or height. Render annotations, LCD
text, and anti-aliasing only when the output requires them.

Use stream save overloads when writing to HTTP responses, cloud object streams, or other non-file destinations. This avoids temporary output files and keeps ownership of the destination stream with the caller.
