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
make benchmark-bmp
make benchmark-pipeline
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
- Fast PNG dispatcher batches using independent seekable and non-seekable stream inputs
- Isolated non-seekable PDF loading allocation
- Row-by-row versus contiguous BMP pixel output
- Sequential versus two-buffer pipelined multi-page PNG and JPEG output
- Managed versus PDFium-owned save buffers at 96, 144, and 300 DPI

Use Release builds and compare allocated bytes as well as mean runtime. Some legacy image-output benchmarks call
`MemoryStream.ToArray()` so BenchmarkDotNet can consume the result; that final byte-array allocation belongs to the
benchmark harness rather than the streaming writer.

## Recorded Local Baseline

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

### Multi-Page Encoding Pipeline

The multi-page benchmark renders four pages serially and writes encoded bytes to counting streams, excluding file
system variance. The optimized path reuses two full page buffers and permits two completed pages to encode while
PDFium rendering remains serialized:

| Output | DPI | Sequential | Two-buffer pipeline | Improvement |
|---|---:|---:|---:|---:|
| Fast PNG | 96 | 69.41 ms | 42.21 ms | 39% |
| Default PNG | 96 | 89.79 ms | 52.95 ms | 41% |
| JPEG quality 100 | 96 | 23.16 ms | 18.19 ms | 21% |
| Fast PNG | 144 | 137.97 ms | 77.94 ms | 44% |
| Default PNG | 144 | 180.55 ms | 99.55 ms | 45% |
| JPEG quality 100 | 144 | 34.48 ms | 24.52 ms | 29% |

The pipeline is automatic for multi-page PNG, JPEG, and WebP output. It does not alter compression or quality. BMP
and one-page output remain sequential because parallel encoding would add scheduling and memory costs without a
comparable CPU-encoding benefit.

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

### Seekable Stream Dispatcher

The independent-stream benchmark submits four seekable `MemoryStream` inputs and writes fast PNG output through a
dispatcher with two encoding workers. The bounded stream reader measured 38.39 ms per batch, compared with 38.26 ms
before the change. The difference is below 1% and well inside the short-run confidence intervals.

The expanded stream benchmark measured 38.44 ms for a four-request seekable batch and 38.53 ms for an equivalent
non-seekable batch after the single-buffer change. The 0.2% difference is below benchmark noise, so buffering no longer
adds a material throughput penalty for this input and render workload.

Managed allocation for this end-to-end benchmark varies with thread-pool scheduling and does not isolate the stream
reader. Save jobs now keep rendered pixels in PDFium-owned unmanaged buffers. The enforced input-memory invariant is
deterministic: all seekable PDF inputs share one lazily rented scratch buffer, and native read requests larger than
64 KiB are copied in chunks. A PDFium callback can therefore no longer rent a temporary managed array proportional to
the requested block size.

### Non-Seekable Stream Loading

The isolated loading benchmark copies the tracked 144,199-byte PDF from a non-seekable stream, opens the document, and
closes it without rendering. Loading directly from the `MemoryStream` backing array removes the second full-size array
previously created by `ToArray()`:

| Implementation | Mean | Managed allocation |
|---|---:|---:|
| Buffered stream plus `ToArray()` | 152.0 us | 542.44 KB |
| Direct buffered backing array | 118.0 us | 385.13 KB |

Managed allocation fell by 157.31 KB per load, or 29%, which exceeds one logical input length. The short-run mean also
improved by about 22%, though the allocation reduction is the acceptance metric. Non-seekable input still requires one
contiguous managed buffer for PDFium random access, and that buffer remains pinned for the document lifetime.

### BMP Output

The isolated writer benchmark uses a 1200 by 1600 bitmap and a counting destination stream. Writing the pixel buffer
once measured 9.49 ns, compared with 1.011 us for 1600 row writes. This removes almost all per-row stream-dispatch
overhead and reduces each BMP from `height + 1` writes to two writes. The benchmark deliberately excludes file-system
I/O; end-to-end gains depend on the destination stream and storage device.

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

BMP output writes the contiguous `PdfBitmap.Pixels` buffer directly after its header. This avoids a stream call for
every bitmap row without allocating another full-size image.

`SaveDocument`, `SavePages`, and `SavePageNumbers` open the PDF once. Multi-page PNG, JPEG, and WebP output overlaps
serialized rendering with two encoding workers and retains at most two reusable PDFium-owned bitmap leases. BMP and
single-page output retain one native lease and run sequentially.

`SavePage`, `SavePageNumber`, `SaveDocument`, `SavePages`, and `SavePageNumbers` render into PDFium-owned unmanaged
buffers when writing directly to files or streams. PNG, JPEG, and WebP encode directly from those pixels. BMP uses a
bounded 64 KiB unmanaged span per stream write. Modern streams consume that span directly; the .NET compatibility
fallback may rent bounded scratch storage. No save helper allocates or rents a managed array proportional to page
pixels.

Grayscale conversion uses PDFium grayscale rendering and skips a managed post-processing pass. Black-and-white conversion also renders through PDFium grayscale, then applies a threshold pass using the grayscale channel instead of recalculating RGB luminance for every pixel.

`PdfPage.Render(PdfBitmap, PdfPageRenderOptions)` and path-based `PdfImageConverter.RenderPageInto` overloads let callers reuse a destination bitmap when the output dimensions stay stable. `PdfBitmapLease` can rent that destination buffer from `ArrayPool<byte>`; dispose the lease as soon as the bitmap is no longer needed, and do not retain its pixel array afterward.

`PdfPage.Render(PdfBitmapLease, PdfPageRenderOptions)` additionally keeps the leased pixels pinned and reuses one
native PDFium bitmap for the lease lifetime. Direct save helpers use this path internally. When repeatedly calling the
converter facade, use the `PdfDocument` overloads to keep the document open rather than paying initialization and file
parsing costs for every page.

`PdfDocument.PageCount` and loaded page dimensions are cached for the native document and page lifetimes. Background
fill and page rendering are also issued within one serialized native operation.

Seekable stream access reuses one process-wide 64 KiB scratch buffer for PDFium custom file callbacks. Larger native
read requests are fulfilled in chunks, which bounds temporary input-copy memory independently of PDF size. This is
safe because PDFium calls and their custom file callbacks are serialized through the shared native lock.

Non-seekable stream access loads PDFium directly from the logical byte range of its buffered `MemoryStream` backing
array. This avoids allocating a second full-document byte array while preserving the contiguous memory and lifetime
PDFium requires.

`PdfRenderSession` packages the lowest-allocation repeated-render path measured by the benchmark suite: it keeps the
document open, caches the current loaded page, and resizes its pooled native bitmap only when output dimensions
change. Use the scoped callback overload when the pixels can be consumed synchronously; use the owned-bitmap or
caller-destination overload when pixels must outlive the call.

Run `make benchmark-native-buffer` to compare the former managed save-buffer design with the PDFium-owned buffer used
by save-only workflows. Keep the native path only while it avoids full-page managed buffers without a material
throughput regression.

For a document-level cold-run comparison, use a local representative PDF:

```bash
make benchmark-all-pages PDF="tests/PdfiumRaster.Tests/TestAssets/annotations.pdf"
```

This opens the document once per separate process, renders and encodes every page sequentially, and reports CSV rows
for both buffer strategies at 96, 144, and 300 DPI in every output format. Metrics include elapsed milliseconds,
encoded bytes, total and maximum page-pixel bytes, managed allocation, sampled peak-working-set growth, and GC counts.
The destination is a counting stream, so the result measures rendering and encoding without filesystem throughput.
Compare a reverse-order or repeated sweep before treating a small timing difference as meaningful.

The 2.0.0 gate was measured on an Apple M3 Pro with .NET 10.0.5 using the tracked annotation PDF and
BenchmarkDotNet's short-run job. Ratios below compare native to managed mean time, grouped across 96, 144, and 300 DPI:

| Format | Geometric mean ratio | Gate |
| --- | ---: | --- |
| BMP | 1.01x | Pass |
| PNG | 1.02x | Pass |
| JPEG | 1.01x | Pass |
| WebP | 0.99x | Pass |

All format-level ratios remain within the 1.05x maximum. Individual short-run measurements can be noisy, especially
at 300 DPI, so retain the generated BenchmarkDotNet reports when investigating a regression. The structural memory
gain is independent of the diagnoser's warmed `ArrayPool` accounting: save-only workflows no longer retain a managed
array sized to the rendered page, and BMP writes at most 64 KiB per call.

`PdfRenderDispatcher` bounds mixed-document concurrency. Its request queue stores descriptors and references, while
`EncodingConcurrency` bounds full-size leased save buffers. With two encoding workers, at most two rendered save
buffers are retained by the pipeline. Raw `PdfBitmap` results become caller-owned and are no longer controlled by the
dispatcher after task completion.

## Usage Guidance

Prefer file path or seekable stream APIs for large PDFs. Byte array and Base64 APIs keep the entire PDF in managed memory.

Use `SaveDocument` for full-document export instead of repeatedly calling single-page helpers. It opens the document
once and automatically pipelines compressed output. Single-page save helpers are optimized for memory, but still
reopen the document each time.

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
