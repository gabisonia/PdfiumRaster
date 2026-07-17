# PdfiumRaster Architecture

This document describes runtime behavior and the design constraints that contributors need to preserve. For usage,
start with the [README](../README.md) or [API guide](API.md).

## Project Purpose

PdfiumRaster is a focused PDF-to-image library. Its scope is rendering PDF pages to bitmap images, saving rendered pages, and exposing the page metadata needed for those workflows.

The project does not aim to provide PDF editing, text extraction, form filling, signing, or viewer UI features.

## Repository Structure

```text
src/PdfiumRaster/                  library source
tests/PdfiumRaster.Tests/          tests and rendering coverage
tests/PdfiumRaster.Tests/TestAssets/
samples/                           sample scenarios
docs/                              API, architecture, and release docs
.github/workflows/                 CI and NuGet publishing workflows
Directory.Packages.props           central package versions
Makefile                           standard local command surface
```

The library targets `netstandard2.0`. Tests target modern .NET and exercise the package through the public API whenever possible.

## Main Components

`PdfImageConverter` is the high-level facade. It covers the common workflows: page count, page sizes, render a page, render by 1-based page number, render selected pages, render all pages, and save pages as images.

`PdfDocument` and `PdfPage` are the lower-level document/page API. Use these when a caller needs direct control over document lifetime, page lifetime, target bitmap placement, or manual rendering.

`PdfRenderSession` is the optimized repeated-operation API. It owns PDFium initialization and the document, caches
the current loaded page, and keeps one correctly sized `PdfBitmapLease` for scoped rendering and saving. It is
synchronous and permits one operation at a time.

`PdfRenderDispatcher` is the mixed-document concurrency API. Multiple producers submit to a bounded channel, one
consumer performs PDFium work, and a configurable number of workers encode completed bitmaps. It provides async
backpressure without weakening the process-wide native serialization rule.

`PdfiumNative` is the native boundary. PDFium P/Invoke declarations and platform-specific native loading rules should stay centralized there.

`PdfBitmap` is the managed representation of rendered BGRA pixels. It carries width, height, stride, and the pixel buffer used by render and image-writing code.

`PdfBitmapLease` owns an `ArrayPool<byte>` buffer. On first native render it pins that buffer and creates an
`FPDF_BITMAP`; subsequent renders reuse both until disposal. A lease keeps a PDFium initialization reference alive
for the native bitmap lifetime.

`PdfImageWriter` writes existing `PdfBitmap` instances. BMP is written directly by PdfiumRaster. PNG, JPEG, and WebP are encoded through SkiaSharp.

Options are split by responsibility:

- `PdfPageRenderOptions` controls rendering size, DPI, rotation, background, anti-aliasing, and PDFium render flags.
- `PdfImageEncodingOptions` controls JPEG/WebP quality and optional PNG compression.
- `PdfImageConversionOptions` composes render, encoding, output format, color mode, and black-and-white settings.

## Rendering Flow

A typical conversion follows this path:

```text
PDF input -> PdfDocument -> PdfPage -> PDFium render -> PdfBitmap -> color mode -> image writer -> destination
```

Inputs can be file paths, byte arrays, streams, or Base64 strings. File paths and seekable streams are preferred for large files because they avoid copying the whole PDF into one managed byte array.

Rendered pages are held as pixel buffers. Memory use grows with page size, DPI, requested width/height, and number of pages held by the caller.

PNG, JPEG, and WebP encoding pins the rendered pixel buffer during synchronous SkiaSharp encoding and writes from an
`SKPixmap` directly to the destination stream. This avoids a second full-size managed pixel buffer and an intermediate
encoded `SKData` buffer.

The high-throughput session path is:

```text
PdfRenderSession -> cached PdfPage -> reusable PdfBitmapLease -> synchronous callback or image writer
```

An owned-bitmap session render still allocates the returned pixel array. Caller-owned and scoped session rendering
avoid that full-size per-call allocation when output dimensions remain stable.

The concurrent save path is:

```text
concurrent callers -> bounded request channel -> one PDFium worker -> bounded bitmap slots -> encoding workers
```

An encoding slot is acquired before rendering a save request. Consequently, slow output cannot cause an unbounded
backlog of full-size rendered pages. Raw bitmap requests bypass encoding slots and transfer their managed pixel memory
to the caller.

## Native Dependencies

Native PDFium binaries are delivered through NuGet runtime assets:

```text
bblanchon.PDFium.Linux
bblanchon.PDFium.macOS
bblanchon.PDFium.Win32
```

SkiaSharp runtime assets are also referenced so PNG, JPEG, and WebP output works after package restore. Consumers should be able to install the package and run basic rendering code without manually copying native binaries.

Native package versions are managed centrally in `Directory.Packages.props`. Do not add package versions directly to project files.

## Large File Behavior

For large PDFs, prefer:

```csharp
PdfImageConverter.SavePng("large.pdf", pageNumber: 1, "page.png");
```

or:

```csharp
using var stream = File.OpenRead("large.pdf");
var pageCount = PdfImageConverter.GetPageCount(stream, leaveOpen: true);
```

Seekable streams use PDFium custom file access and are not copied into one managed byte array. Byte array and Base64 APIs keep the full PDF in memory. Non-seekable streams are buffered because PDFium requires random access.

Large rendered pages can still allocate large pixel buffers. DPI, page dimensions, requested output width/height, and
the number of live output bitmaps matter as much as PDF input size. The pooled backing array may be larger than
`Stride * Height`; only that logical pixel region belongs to the bitmap.

## Production Resource Boundaries

PDFium is native code parsing potentially untrusted input. Applications should keep the managed and native packages
updated and apply limits before rendering: maximum input size, page count, output dimensions, DPI/scale, total pixels,
and request duration. Public numeric validation rejects invalid values but intentionally does not impose an
application-specific maximum. A separately supervised worker process provides stronger crash, timeout, and memory
isolation for hostile or very large documents.

## Threading Model

PDFium is not thread-safe. PdfiumRaster serializes native PDFium calls with a process-wide shared lock.

For application code, this means a single process should not expect true parallel PDFium rendering. `PdfRenderDispatcher`
accepts concurrent callers through a bounded asynchronous channel but uses one native consumer. Image encoding and
destination writing occur after the document is closed and may run concurrently because they do not use PDFium.
Callers must still coordinate the lifetime of disposable document, page, and lease objects; the shared native lock is
not permission to dispose an object while another operation is using it. `PdfRenderSession` adds a per-session
operation guard and rejects concurrent or reentrant operations. If true parallel rendering is required, use multiple
processes and keep each process isolated.

## Test Strategy

The normal test suite uses tracked assets and excludes local-only tests:

```bash
make test
```

Local-only tests use ignored assets such as `tests/PdfiumRaster.Tests/TestAssets/annotations.pdf` and are marked with `Category=Local`:

```bash
make test-local
```

The ignored local PDF is useful for validating private or larger annotation-heavy documents without committing them. CI runs the non-local test suite and uses tracked test assets only.

Rendering tests write generated images under the test output directory, for example:

```text
tests/PdfiumRaster.Tests/bin/Debug/net10.0/TestOutput/
```

## Packaging And Release Model

The NuGet package includes:

- `lib/netstandard2.0/PdfiumRaster.dll`
- XML documentation for IntelliSense
- `README.md` as the package readme
- symbol package output
- transitive native PDFium and SkiaSharp runtime assets

Releases are documented in [RELEASING.md](RELEASING.md). Publishing is handled by the manual GitHub Actions workflow using NuGet Trusted Publishing.

Before release-related changes are considered complete, run:

```bash
make test
make pack
make inspect-package
```

## Performance Measurement

Performance benchmarks live in `benchmarks/PdfiumRaster.Benchmarks` and are run with:

```bash
make benchmark
```

Focused suites are also available:

```bash
make benchmark-session
make benchmark-encoding
make benchmark-compare
make benchmark-dispatcher
```

Benchmark guidance and current memory notes are documented in [PERFORMANCE.md](PERFORMANCE.md).

Selected-page exports should use `SavePages` or `SavePageNumbers` so the PDF is opened once and pages are still processed one at a time.

## Contributing

Native lifetime, compatibility, documentation, and test requirements are documented in
[CONTRIBUTING.md](../CONTRIBUTING.md).
