# PdfiumRaster Performance

This document tracks how PdfiumRaster measures and improves rendering performance and memory use.

## Goals

The primary optimization target is lower memory use during PDF-to-image conversion. Speed improvements are useful, but changes should not increase peak memory substantially or weaken native lifetime safety.

## Benchmarking

Run benchmarks with:

```bash
make benchmark
```

Run focused suites with:

```bash
make benchmark-session
make benchmark-encoding
make benchmark-compare
make benchmark-dispatcher
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
- Legacy reopen versus owned, caller-owned, and scoped `PdfRenderSession` rendering at 96, 144, and 300 DPI
- Isolated PNG encoding at the Skia default and zlib compression levels 1, 6, and 9
- Sequential batches versus `PdfRenderDispatcher` batches for raw rendering and fast PNG output

Use Release builds and compare allocated bytes as well as mean runtime. Some legacy image-output benchmarks call
`MemoryStream.ToArray()` so BenchmarkDotNet can consume the result; that final byte-array allocation belongs to the
benchmark harness rather than the streaming writer.

## Latest Local Baseline

These results were measured in July 2026 on an Apple M3 Pro using macOS 26.5.2, .NET SDK 10.0.105, and .NET runtime
10.0.5. They use the tracked `axf-annotation-1.pdf` asset. Results are a regression baseline for this machine, not a
cross-platform performance guarantee.

### Reusable Session Rendering

| Method | DPI | Mean | Managed allocation | Relative to reopen |
|---|---:|---:|---:|---:|
| Reopen and render | 96 | 2.923 ms | 3,567,350 B | 1.00x |
| Session, owned bitmap | 96 | 1.438 ms | 3,567,460 B | 0.49x |
| Session, caller-owned bitmap | 96 | 1.176 ms | 25 B | 0.40x |
| Session, scoped bitmap | 96 | 1.179 ms | 0 B | 0.40x |
| Reopen and render | 144 | 3.770 ms | 8,023,283 B | 1.00x |
| Session, owned bitmap | 144 | 2.149 ms | 8,023,433 B | 0.57x |
| Session, caller-owned bitmap | 144 | 1.558 ms | 25 B | 0.41x |
| Session, scoped bitmap | 144 | 1.551 ms | 0 B | 0.41x |
| Reopen and render | 300 | 8.316 ms | 34,814,059 B | 1.00x |
| Session, owned bitmap | 300 | 6.573 ms | 34,814,126 B | 0.79x |
| Session, caller-owned bitmap | 300 | 3.755 ms | 26 B | 0.45x |
| Session, scoped bitmap | 300 | 3.745 ms | 0 B | 0.45x |

The scoped session path was 2.2x to 2.5x faster than reopening the PDF and eliminated the full-size output allocation.
The caller-owned path had effectively the same rendering speed. The owned session path avoids reopening and parsing,
but must still allocate the returned pixel buffer.

The session fast-PNG save benchmark, which includes rendering, encoding, and a new in-memory destination, measured
15.221 ms at 96 DPI, 32.543 ms at 144 DPI, and 134.080 ms at 300 DPI.

### PNG Compression

The isolated encoder benchmark uses a pre-rendered 144-DPI bitmap:

| PNG compression | Mean | Managed allocation |
|---|---:|---:|
| Skia default | 42.60 ms | 508.34 KB |
| Level 1 | 30.70 ms | 508.34 KB |
| Level 6 | 42.60 ms | 508.34 KB |
| Level 9 | 110.15 ms | 508.34 KB |

Level 1 was about 28% faster than the Skia default for this document. Compression level changes can affect encoded
size differently for different page content, so benchmark both time and output size on the production corpus before
choosing a global setting.

The reported encoding allocation is dominated by the `MemoryStream` destination buffer. Writing to a file, network
response, or other streaming destination avoids retaining the complete encoded image in a managed byte array; it does
not eliminate allocations performed by the destination itself.

### Concurrent Dispatcher

The dispatcher short-run benchmark uses batches of four independent 96-DPI requests. Fast-PNG output uses two
encoding workers and a counting destination stream so encoded bytes are consumed without retaining them:

| Method | Batch mean | Managed allocation | Relative throughput |
|---|---:|---:|---:|
| Sequential fast PNG | 69.11 ms | 3.59 KB | 1.00x |
| Dispatcher fast PNG | 38.66 ms | 7.17 KB | 1.79x |
| Sequential raw bitmap | 12.49 ms | 13.61 MB | 1.00x |
| Dispatcher raw bitmap | 12.30 ms | 13.61 MB | Effectively tied |

Parallel encoding reduced the four-image PNG batch time by about 44%. The additional small managed allocation is
dispatcher task and queue bookkeeping. Raw rendering did not materially improve because both paths execute the same
PDFium work serially; its confidence intervals overlap. These short-run results validate the pipeline but should not
be used to select production capacity without workload-specific measurements.

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

The latest comparison measured 1.543 ms for PdfiumRaster and 1.545 ms for PDFiumCore, with zero managed allocation
per render. The confidence intervals overlap, so the wrappers are effectively tied under equivalent hot-render
conditions. Treat these numbers as a local baseline and rerun the benchmark on every target platform and
representative PDF corpus.

## Current Memory Optimizations

PNG, JPEG, and WebP encoding pins the existing `PdfBitmap.Pixels` buffer and encodes directly from an `SKPixmap` to
the destination stream. This avoids allocating a second full-size image and an intermediate `SKData` encoded buffer.

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

`PdfRenderSession` packages the fastest safe repeated-render path: it keeps the document open, caches the current
loaded page, and resizes its pooled native bitmap only when output dimensions change. Use the scoped callback overload
when the pixels can be consumed synchronously; use the owned-bitmap or caller-destination overload when pixels must
outlive the call.

`PdfRenderDispatcher` bounds mixed-document concurrency. Its request queue stores descriptors and references, while
`EncodingConcurrency` bounds full-size leased save buffers. With two encoding workers, at most two rendered save
buffers are retained by the pipeline. Raw `PdfBitmap` results become caller-owned and are no longer controlled by the
dispatcher after task completion.

## Usage Guidance

Prefer file path or seekable stream APIs for large PDFs. Byte array and Base64 APIs keep the entire PDF in managed memory.

Use `SaveDocument` for full-document export instead of repeatedly calling single-page helpers. Single-page save helpers are optimized for memory, but still reopen the document each time.

Use `SavePages` or `SavePageNumbers` when exporting only part of a document.

Output dimensions are the primary rendering cost. The default 300 DPI is intended for high-resolution output. Use an
explicit 72 or 96 DPI for thumbnails and previews, or set an explicit pixel width or height. Render annotations, LCD
text, and anti-aliasing only when the output requires them.

`PdfPageRenderOptions.ScreenPreview` selects 96 DPI. `PdfImageEncodingOptions.Fast` selects PNG compression level 1
and JPEG/WebP quality 85. These presets are opt-in; existing 300 DPI and encoder defaults remain unchanged. The fast
PNG setting may produce larger files, while JPEG/WebP quality 85 uses lossy compression.

Use stream save overloads when writing to HTTP responses, cloud object streams, or other non-file destinations. This avoids temporary output files and keeps ownership of the destination stream with the caller.

For unrelated concurrent requests, share one `PdfRenderDispatcher`. A larger `QueueCapacity` absorbs bursts but may
retain more input byte arrays or streams while jobs wait. Raising `EncodingConcurrency` can improve compressed-output
throughput but also increases CPU use and permits one additional full rendered page per worker. Measure the dispatcher
benchmark against the production PDF corpus before changing either default.

The dispatcher cannot improve raw PDFium render throughput in one process because native work remains serialized. If
raw rendering is the bottleneck after measuring encoding separately, use multiple supervised processes rather than
increasing in-process task concurrency.
