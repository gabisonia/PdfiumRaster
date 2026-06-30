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

Use Release builds and compare allocated bytes as well as mean runtime. For image output benchmarks, remember that `MemoryStream.ToArray()` allocates the final encoded byte array so the result can be consumed by BenchmarkDotNet.

## Current Memory Optimizations

PNG, JPEG, and WebP encoding pins the existing `PdfBitmap.Pixels` buffer and passes it directly to SkiaSharp during synchronous encoding. This avoids allocating and copying a second full-size managed pixel buffer before encoding.

BMP output writes rows directly from the existing `PdfBitmap.Pixels` buffer.

`SaveDocument` opens the PDF once, caches the page count, and processes pages one at a time. This avoids retaining rendered bitmaps for the entire document export.

`SavePage`, `SavePageNumber`, `SaveDocument`, `SavePages`, and `SavePageNumbers` render through pooled page bitmaps when writing directly to files or streams. Prefer save helpers when the rendered bitmap does not need to be returned to the caller.

Grayscale conversion uses PDFium grayscale rendering and skips a managed post-processing pass. Black-and-white conversion also renders through PDFium grayscale, then applies a threshold pass using the grayscale channel instead of recalculating RGB luminance for every pixel.

`PdfPage.Render(PdfBitmap, PdfPageRenderOptions)` and path-based `PdfImageConverter.RenderPageInto` overloads let callers reuse a destination bitmap when the output dimensions stay stable. `PdfBitmapLease` can rent that destination buffer from `ArrayPool<byte>`; dispose the lease as soon as the bitmap is no longer needed, and do not retain its pixel array afterward.

## Usage Guidance

Prefer file path or seekable stream APIs for large PDFs. Byte array and Base64 APIs keep the entire PDF in managed memory.

Use `SaveDocument` for full-document export instead of repeatedly calling single-page helpers. Single-page save helpers are optimized for memory, but still reopen the document each time.

Use `SavePages` or `SavePageNumbers` when exporting only part of a document.

Use stream save overloads when writing to HTTP responses, cloud object streams, or other non-file destinations. This avoids temporary output files and keeps ownership of the destination stream with the caller.
