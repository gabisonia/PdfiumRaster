# PdfiumRaster API Reference

This document summarizes the supported public API surface.

For project structure, runtime architecture, native dependency behavior, and contributor guidance, see [Architecture](ARCHITECTURE.md).

For memory and benchmark guidance, see [Performance](PERFORMANCE.md).

## Primary Facade

Use `PdfImageConverter` for most workflows.

### Page Metadata

```csharp
int pageCount = PdfImageConverter.GetPageCount("input.pdf");
IReadOnlyList<PdfPageSize> sizes = PdfImageConverter.GetPageSizes("input.pdf");
```

Supported inputs:

```csharp
PdfImageConverter.GetPageCount(string pdfPath);
PdfImageConverter.GetPageCount(byte[] pdfBytes);
PdfImageConverter.GetPageCount(Stream pdfStream);
PdfImageConverter.GetPageCountFromBase64(string pdfBase64);

PdfImageConverter.GetPageSizes(string pdfPath);
PdfImageConverter.GetPageSizes(byte[] pdfBytes);
PdfImageConverter.GetPageSizes(Stream pdfStream);
PdfImageConverter.GetPageSizesFromBase64(string pdfBase64);
```

`PdfPageSize.Width` and `PdfPageSize.Height` are in PDF points.

### Large PDF Inputs

For large documents, prefer path-based APIs or seekable streams:

```csharp
using var stream = File.OpenRead("large.pdf");
int pageCount = PdfImageConverter.GetPageCount(stream, leaveOpen: true);
```

Seekable streams are loaded through PDFium custom file access, so the full PDF is not copied into a managed byte array. Byte array and Base64 APIs keep the full PDF in memory. Non-seekable streams are buffered into memory before loading because PDFium requires random access.

Input stream overloads use `leaveOpen: false` by default. Pass `leaveOpen: true` when ownership remains with the
caller. Output stream overloads always leave the destination stream open.

## Rendering

Zero-based page indexes:

```csharp
PdfBitmap bitmap = PdfImageConverter.RenderPage("input.pdf", pageIndex: 0);
```

One-based page numbers:

```csharp
PdfBitmap bitmap = PdfImageConverter.RenderPageNumber("input.pdf", pageNumber: 1);
```

Supported inputs:

```csharp
PdfImageConverter.RenderPage(string pdfPath, int pageIndex);
PdfImageConverter.RenderPage(byte[] pdfBytes, int pageIndex);
PdfImageConverter.RenderPage(Stream pdfStream, int pageIndex);
PdfImageConverter.RenderPageFromBase64(string pdfBase64, int pageIndex);
PdfImageConverter.RenderPage(PdfDocument document, int pageIndex);

PdfImageConverter.RenderPageNumber(string pdfPath, int pageNumber);
PdfImageConverter.RenderPageNumber(byte[] pdfBytes, int pageNumber);
PdfImageConverter.RenderPageNumber(PdfDocument document, int pageNumber);
```

Render a path-based page into an existing bitmap:

```csharp
PdfImageConverter.RenderPageInto(string pdfPath, int pageIndex, PdfBitmap destination);
PdfImageConverter.RenderPageNumberInto(string pdfPath, int pageNumber, PdfBitmap destination);
PdfImageConverter.RenderPageInto(PdfDocument document, int pageIndex, PdfBitmap destination);
PdfImageConverter.RenderPageNumberInto(PdfDocument document, int pageNumber, PdfBitmap destination);
```

Render selected zero-based pages:

```csharp
IEnumerable<PdfBitmap> pages = PdfImageConverter.RenderPages("input.pdf", new[] { 0, 2, 4 });
```

Render all pages:

```csharp
IEnumerable<PdfBitmap> pages = PdfImageConverter.RenderDocument("input.pdf");
```

Returned `PdfBitmap` instances are caller-owned managed objects. Pixels use BGRA byte order, `Stride` is measured in
bytes per row, and the pixel buffer occupies at least `Stride * Height` bytes. Rendering and memory cost grow with
the output pixel count.

## Reusable Render Sessions

`PdfRenderSession` owns PDFium initialization, the open document, the current loaded page, and a correctly sized
pooled render buffer. Use it for repeated operations on one PDF:

```csharp
using var session = PdfRenderSession.Open("input.pdf");
var options = new PdfImageConversionOptions
{
    Render = PdfPageRenderOptions.ScreenPreview,
};

PdfBitmap owned = session.RenderPage(pageIndex: 0, options: options);
var destination = PdfBitmap.Create(owned.Width, owned.Height);
session.RenderPageInto(pageIndex: 0, destination: destination, options: options);

int width = session.RenderPage(pageIndex: 0, callback: bitmap => bitmap.Width, options: options);
byte firstBlue = session.RenderPage(pageIndex: 0, callback: bitmap => bitmap.Pixels[0], options: options);
```

Session page indexes are zero-based. `RenderPage` returning `PdfBitmap` allocates an independently owned pixel buffer.
The callback overloads reuse the session buffer; the bitmap and its pixels are valid only until the callback returns
and must not be retained. Callbacks are synchronous. Their exceptions propagate, and the session remains usable.

A session accepts one operation at a time. Concurrent or reentrant operations throw `InvalidOperationException`.
This includes trying to use or dispose the session from inside its callback. Dispose the session after use. Path,
byte-array, and stream inputs are supported:

```csharp
PdfRenderSession.Open(string pdfPath, string? password = null);
PdfRenderSession.Open(byte[] pdfBytes, string? password = null);
PdfRenderSession.Open(Stream pdfStream, bool leaveOpen = false, string? password = null);
```

Seekable streams use random access without a full managed copy. Non-seekable streams are buffered. Byte arrays remain
pinned and fully resident until the session is disposed.

`SavePage` has file-path and stream overloads and uses the session-owned reusable buffer internally. Destination
streams remain open:

```csharp
using var output = File.Create("page.png");
session.SavePage(pageIndex: 0, imageStream: output, options: new PdfImageConversionOptions
{
    Render = PdfPageRenderOptions.ScreenPreview,
    Format = PdfImageOutputFormat.Png,
    Encoding = PdfImageEncodingOptions.Fast,
});
```

## Concurrent Render Dispatcher

`PdfRenderDispatcher` is the high-level API for concurrent requests involving unrelated PDFs. Share one dispatcher
for the application lifetime instead of constructing one per request:

```csharp
using var dispatcher = new PdfRenderDispatcher(new PdfRenderDispatcherOptions
{
    QueueCapacity = 42,
    EncodingConcurrency = 2,
    QueueFullMode = PdfRenderQueueFullMode.Wait,
});

Task<PdfBitmap> bitmap = dispatcher.RenderPageAsync("input.pdf", pageIndex: 0);
Task save = dispatcher.SavePageAsync("other.pdf", pageIndex: 0, "page.png", new PdfImageConversionOptions
{
    Render = PdfPageRenderOptions.ScreenPreview,
    Format = PdfImageOutputFormat.Png,
    Encoding = PdfImageEncodingOptions.Fast,
});

await Task.WhenAll(bitmap, save);
await dispatcher.CompleteAsync();
```

`RenderPageAsync` and `SavePageAsync` have PDF input overloads for `string`, `byte[]`, and `Stream`.
`SavePageAsync` writes to either an image path or a caller-owned output stream. All page indexes are zero-based.
Returned bitmaps have independent managed pixel buffers. Output streams remain open.

The bounded queue has two modes:

- `Wait` asynchronously waits for capacity and is the default.
- `Reject` faults a full-queue submission with `PdfRenderQueueFullException`.

`CompleteAsync()` stops new submissions and drains accepted jobs. `CancelAsync()` cancels work that has not entered an
uninterruptible stage and waits for active stages. `Dispose()` uses cancellation shutdown and waits synchronously.
PDFium rendering and Skia encoding already in progress cannot be interrupted.

Conversion options and their nested render/encoding options are copied at submission. PDF byte arrays are referenced,
not copied, and must remain unchanged through completion. A PDF stream must remain usable and unmodified until its
task completes; `leaveOpen: false` transfers disposal responsibility after the job is accepted. If submission is
canceled while waiting for queue capacity, ownership does not transfer. Input and output cannot be the same stream.
Do not write to an output stream or submit it to another concurrent job until its task completes.

The dispatcher does not remove the process-wide native lock. PDF load/render operations remain serialized, while
completed PNG, JPEG, WebP, or BMP writes may overlap up to `EncodingConcurrency`. At most that many rendered save
buffers are retained by the pipeline. Encoded requests can therefore complete out of submission order. Use
`PdfRenderSession` for repeated pages from one document and multiple supervised processes for true PDFium parallelism.

## Saving Images

Save with an explicit format:

```csharp
PdfImageConverter.SavePageNumber("input.pdf", pageNumber: 1, "page.bmp", new PdfImageConversionOptions
{
    Format = PdfImageOutputFormat.Bmp,
});
```

Convenience methods:

```csharp
PdfImageConverter.SavePng("input.pdf", pageNumber: 1, "page.png");
PdfImageConverter.SaveJpeg("input.pdf", pageNumber: 1, "page.jpg");
PdfImageConverter.SaveWebp("input.pdf", pageNumber: 1, "page.webp");
```

Save every page:

```csharp
int pageCount = PdfImageConverter.SaveDocument("input.pdf", "images", fileNamePrefix: "page");
```

Save selected zero-based page indexes or one-based page numbers while opening the PDF once:

```csharp
PdfImageConverter.SavePages("input.pdf", new[] { 0, 2 }, "images");
PdfImageConverter.SavePageNumbers("input.pdf", new[] { 1, 3 }, "images");
```

Generated names use 1-based numbering:

```text
page-0001.bmp
page-0002.bmp
page-0003.bmp
```

`RenderDocument`, `RenderPages`, `SaveDocument`, `SavePages`, and `SavePageNumbers` open the input document once for
the operation. Single-page path helpers reopen the PDF on every call; prefer `PdfRenderSession` for repeated
latency-sensitive single-page work.

## Output Formats

```csharp
public enum PdfImageOutputFormat
{
    Bmp,
    Png,
    Jpeg,
    Webp,
}
```

BMP is written directly by PdfiumRaster. PNG, JPEG, and WebP are encoded with SkiaSharp.

## Conversion Options

```csharp
var options = new PdfImageConversionOptions
{
    Format = PdfImageOutputFormat.Png,
    ColorMode = PdfImageColorMode.BlackAndWhite,
    BlackAndWhiteThreshold = 160,
    Encoding = new PdfImageEncodingOptions
    {
        Quality = 85,
        PngCompressionLevel = 1,
    },
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
};
```

`Quality` applies to JPEG and lossy WebP and accepts 0 through 100. `PngCompressionLevel` accepts 0 through 9; its
default `null` preserves Skia's default compression. `PdfImageEncodingOptions.Fast` returns a new instance with
quality 85 and PNG compression level 1. The preset prioritizes speed; lower PNG compression can increase file size.
Existing defaults remain quality-oriented.

### Defaults

| Setting | Default |
|---|---|
| Render DPI | 300 |
| Scale | 1 |
| Render flags | `Annot | LcdText` |
| Anti-aliasing | `All` |
| Background | Opaque white (`0xFFFFFFFF`) |
| Output format | BMP |
| Color mode | Color |
| JPEG/WebP quality | 100 |
| PNG compression | Skia default |

`PdfPageRenderOptions.ScreenPreview` returns a new 96-DPI options instance on every access.

When `FillBackground` is `false`, PDFium renders without a pre-fill. Newly allocated bitmaps start cleared, but an
existing caller-owned bitmap or lease can retain pixels from an earlier render wherever the page does not paint.
Clear reusable destinations explicitly when transparent output must start from zero.

### Output Sizing

Without explicit dimensions, output pixels are calculated from PDF points, DPI, and scale. PDF points use 72 units
per inch. Set `Width` or `Height` for an explicit pixel dimension. When `WithAspectRatio` is `true`, set exactly one
of `Width` and `Height`; the other dimension is calculated from the page aspect ratio. A 90- or 270-degree rotation
swaps the final output width and height.

### Color Modes

```csharp
public enum PdfImageColorMode
{
    Color,
    Grayscale,
    BlackAndWhite,
}
```

`BlackAndWhiteThreshold` is applied to luminance after rendering. Lower values produce more white pixels; higher values produce more black pixels.

### Anti-Aliasing

```csharp
public enum PdfAntiAliasing
{
    None,
    Text,
    Images,
    Paths,
    All,
}
```

Use bitwise combinations when needed:

```csharp
AntiAliasing = PdfAntiAliasing.Text | PdfAntiAliasing.Paths
```

## Manual Bitmap Rendering

Use `PdfDocument` and `PdfPage` when you need placement control.

```csharp
using var pdfium = PdfiumLibrary.Initialize();
using var document = PdfDocument.Load("input.pdf");
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
```

When rendering the same page size repeatedly, render options can also be used with an existing bitmap:

```csharp
var render = new PdfPageRenderOptions { Dpi = 144 };
var (width, height) = render.GetPixelSize(page.Width, page.Height);
var reusable = PdfBitmap.Create(width, height);

page.Render(reusable, render);
```

For repeated pages from one PDF, keep the document open and use the document-scoped facade overload. This avoids
reinitializing PDFium and reopening the file for each page:

```csharp
using var pdfium = PdfiumLibrary.Initialize();
using var document = PdfDocument.Load("input.pdf");

var render = new PdfPageRenderOptions { Dpi = 144 };

for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
{
    PdfBitmap bitmap = PdfImageConverter.RenderPage(document, pageIndex, render);
    PdfImageWriter.SaveJpeg(bitmap, $"page-{pageIndex + 1:D4}.jpg", quality: 85);
}
```

The converter facade also supports path-based rendering into an existing bitmap:

```csharp
var options = new PdfImageConversionOptions { Render = render };

PdfImageConverter.RenderPageInto("input.pdf", pageIndex: 0, reusable, options);
```

For repeated rendering where the bitmap does not need to outlive the render operation, use a pooled bitmap lease:

```csharp
using var lease = PdfBitmapLease.Rent(width, height, clear: false);

page.Render(lease, render);
PdfImageWriter.SavePng(lease.Bitmap, "page.png");
```

The `PdfPage.Render(PdfBitmapLease, ...)` overload pins the pooled pixels and creates a native PDFium bitmap on its
first call, then reuses both on subsequent calls. `PdfBitmapLease` keeps PDFium initialized until it destroys that
native bitmap and returns its pixel buffer to the shared array pool. Do not retain `lease.Bitmap` or
`lease.Bitmap.Pixels` after disposal. The pooled pixel array may be larger than `Stride * Height`; image writers use
the bitmap dimensions and stride.

Rendering cost grows approximately with pixel count. For previews, thumbnails, and other screen-oriented output,
explicitly select an appropriate DPI instead of the 300 DPI print-oriented default:

```csharp
var preview = PdfPageRenderOptions.ScreenPreview;
```

## Writing Existing Bitmaps

```csharp
PdfImageWriter.SaveBmp(bitmap, "page.bmp");
PdfImageWriter.SavePng(bitmap, "page.png");
PdfImageWriter.SaveJpeg(bitmap, "page.jpg");
PdfImageWriter.SaveWebp(bitmap, "page.webp");

var fast = PdfImageEncodingOptions.Fast;
PdfImageWriter.SavePng(bitmap, "page-fast.png", fast);
PdfImageWriter.SaveJpeg(bitmap, "page-fast.jpg", fast);
PdfImageWriter.SaveWebp(bitmap, "page-fast.webp", fast);
```

Stream writers are also available:

```csharp
PdfImageWriter.WriteBmp(bitmap, stream);
PdfImageWriter.WritePng(bitmap, stream);
PdfImageWriter.WriteJpeg(bitmap, stream);
PdfImageWriter.WriteWebp(bitmap, stream);
PdfImageWriter.WritePng(bitmap, stream, PdfImageEncodingOptions.Fast);
```

Compressed writers encode directly from the pinned bitmap pixels into the destination stream. Stream overloads do
not close caller-owned streams.

The converter facade also supports saving directly to streams:

```csharp
using var stream = File.Create("page.png");
PdfImageConverter.SavePng("input.pdf", pageNumber: 1, stream);
```

## Native Dependencies

Native PDFium binaries are provided through:

```text
bblanchon.PDFium.Linux
bblanchon.PDFium.macOS
bblanchon.PDFium.Win32
```

The NuGet dependency graph carries the matching native runtime assets to consuming applications. No manual native
binary copy is required for supported runtime identifiers.

## Errors And Threading

Public APIs validate nulls, page ranges, dimensions, DPI, rotation, encoding ranges, and destination bitmap sizes.
PDFium-reported document and page failures raise `PdfiumException`; inspect its `Error` property rather than retrying
blindly.

PDFium is not thread-safe. PdfiumRaster serializes native PDFium calls with a process-wide shared lock. Concurrent
native calls do not render inside PDFium in parallel. Callers must still coordinate ownership of disposable
`PdfDocument`, `PdfPage`, and `PdfBitmapLease` instances and must not dispose them while another operation is using
them. `PdfRenderSession` is synchronous and single-operation; concurrent and reentrant session operations throw
`InvalidOperationException`. Use separate processes for true parallel rendering.

## Current Scope

Supported:

- Desktop/server .NET via `netstandard2.0`
- PDF-to-image rendering
- PDFium native assets through NuGet
- BMP/PNG/JPEG/WebP output
- File, stream, byte array, and Base64 inputs

Not currently implemented:

- Async `IAsyncEnumerable` conversion
- Form-fill rendering
- Bounds/tiling rendering
- Browser/mobile-specific target frameworks
- .NET Framework-specific native loader customization
