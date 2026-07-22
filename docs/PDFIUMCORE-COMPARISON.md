# PdfiumRaster And PDFiumCore

PdfiumRaster and [PDFiumCore](https://github.com/Dtronix/PDFiumCore) both call native PDFium. They are not competing
rendering engines: the difference is how much of the application workflow each library supplies.

PDFiumCore is generated from PDFium headers with CppSharp and exposes the broader C API through .NET types and
functions. PdfiumRaster maintains a small native boundary and builds an owned PDF-to-image workflow around it. When
both use the same native version, page dimensions, background, render flags, and bitmap format, the actual page
rasterization is the same PDFium work.

## Which Library To Choose

| Concern | PdfiumRaster | PDFiumCore |
| --- | --- | --- |
| Primary purpose | Render PDF pages and write images | Bind the broader PDFium C API |
| API style | Documents, pages, sessions, conversion options, and a dispatcher | Functions and handle types that closely follow PDFium |
| Native initialization | Reference-counted by the rendering workflow | Explicitly composed by the application |
| Document and page cleanup | Disposable managed owners | Explicit PDFium close calls |
| File and stream input | Paths, bytes, Base64, seekable callbacks, and buffered non-seekable streams | PDFium load functions; custom access is application code |
| Render memory | Caller-owned, pooled, or PDFium-owned buffers depending on the workflow | PDFium or application buffer chosen by the caller |
| Image output | BMP directly; PNG, JPEG, and WebP through SkiaSharp | Raw PDFium bitmap; encoding is a separate application choice |
| Threading policy | PDFium calls serialized process-wide; encoding may overlap | Application supplies synchronization and scheduling |
| API breadth | Intentionally limited to PDF-to-image conversion | Text, forms, editing, annotations, and other exposed PDFium APIs |
| Best fit | Applications that need reliable image output | Applications building a custom PDFium integration |

Use PdfiumRaster when the desired result is an image or reusable BGRA bitmap. Use PDFiumCore when direct access to
PDFium behavior outside that workflow is the actual requirement. PDFiumCore does not make PDFium inherently faster;
it exposes more of it.

## The Same PNG Workflow

PdfiumRaster owns the conversion pipeline, so a one-page PNG is one operation. Page-number helpers are 1-based:

```csharp
using PdfiumRaster;

PdfImageConverter.SavePng(
    "input.pdf",
    pageNumber: 1,
    "page.png",
    new PdfImageConversionOptions
    {
        Render = new PdfPageRenderOptions { Dpi = 144 },
        Encoding = PdfImageEncodingOptions.Fast,
    });
```

With PDFiumCore, the application assembles the equivalent PDFium and SkiaSharp operations. PDFium page indexes are
0-based, page dimensions are PDF points, the destination dimensions are pixels, and cleanup order matters:

```csharp
using PDFiumCore;
using SkiaSharp;

const int dpi = 144;
const int bgra = 4;
const int annotationsAndLcdText = 0x01 | 0x02;

FpdfDocumentT? document = null;
FpdfPageT? page = null;
FpdfBitmapT? bitmap = null;

fpdfview.FPDF_InitLibrary();
try
{
    document = fpdfview.FPDF_LoadDocument("input.pdf", null)
        ?? throw new InvalidOperationException("Could not load the PDF.");
    page = fpdfview.FPDF_LoadPage(document, 0)
        ?? throw new InvalidOperationException("Could not load page 1.");

    int width = checked((int)Math.Ceiling(fpdfview.FPDF_GetPageWidthF(page) / 72d * dpi));
    int height = checked((int)Math.Ceiling(fpdfview.FPDF_GetPageHeightF(page) / 72d * dpi));
    bitmap = fpdfview.FPDFBitmapCreateEx(width, height, bgra, IntPtr.Zero, 0)
        ?? throw new InvalidOperationException("Could not create the bitmap.");

    fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, width, height, 0xFFFFFFFF);
    fpdfview.FPDF_RenderPageBitmap(
        bitmap, page, 0, 0, width, height, 0, annotationsAndLcdText);

    IntPtr pixels = fpdfview.FPDFBitmapGetBuffer(bitmap);
    int stride = fpdfview.FPDFBitmapGetStride(bitmap);
    var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
    using var pixmap = new SKPixmap(info, pixels, stride);
    using var output = File.Create("page.png");
    var png = new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, zLibLevel: 1);
    if (!pixmap.Encode(output, png))
    {
        throw new InvalidOperationException("Could not encode the PNG.");
    }
}
finally
{
    if (bitmap is not null) fpdfview.FPDFBitmapDestroy(bitmap);
    if (page is not null) fpdfview.FPDF_ClosePage(page);
    if (document is not null) fpdfview.FPDF_CloseDocument(document);
    fpdfview.FPDF_DestroyLibrary();
}
```

The longer version is not a criticism of PDFiumCore. Those explicit choices are useful when an application needs
custom transforms, progressive rendering, form handling, text access, or another part of PDFium. PdfiumRaster makes
the choices needed for its narrower image-conversion contract.

## How PdfiumRaster Does It

The normal conversion flow is:

```text
PDF input
  -> owned document and page
  -> serialized PDFium render
  -> reusable BGRA buffer
  -> optional grayscale or black-and-white processing
  -> BMP writer or SkiaSharp encoder
  -> file or caller-owned output stream
```

- `PdfiumNative` contains the hand-maintained P/Invoke boundary and platform loading rules. Native calls use one
  shared lock because PDFium is not thread-safe.
- `PdfiumLibrary`, `PdfDocument`, and `PdfPage` manage the normal initialization and native-handle lifetimes used by
  the high-level workflows.
- Paths and seekable streams avoid a full managed PDF copy. Seekable streams use PDFium custom file callbacks with a
  bounded shared scratch buffer. Non-seekable streams are buffered once because PDFium requires random access.
- Returned `PdfBitmap` objects own managed BGRA pixels. Repeated-render APIs can reuse pooled managed memory, while
  save-only paths render into PDFium-owned memory and encode directly from it.
- `PdfRenderSession` keeps one document and current page open for repeated synchronous work.
- `PdfRenderDispatcher` bounds concurrent requests, performs PDFium work on one native stage, and overlaps completed
  PNG, JPEG, or WebP encoding when configured with multiple encoding workers.

See [Architecture](ARCHITECTURE.md) for native lifetimes and [Performance](PERFORMANCE.md) for measured memory behavior.

## How PDFiumCore Does It

PDFiumCore's build downloads matching PDFium binaries and headers, runs CppSharp to generate its binding source, and
packages the generated .NET API with native runtime dependencies. An application then calls API families such as
`FPDF_LoadDocument`, `FPDF_LoadPage`, `FPDFBitmapCreateEx`, and `FPDF_RenderPageBitmap` directly.

This provides much broader and faster-moving PDFium coverage than a focused hand-written interop layer. The tradeoff
is that PDFium's contracts remain application concerns: which calls require initialization, which handles must be
closed, how callback state stays rooted, how native calls are serialized, how pixels are owned, and which encoder
consumes the result.

## Why PdfiumRaster Does Not Depend On PDFiumCore

PdfiumRaster uses only a small rendering-related part of PDFium. Depending on the full generated binding package
would not replace its document owners, stream callbacks, serialization, buffer leases, conversion options, encoders,
or pipelines. It would also add a second versioned interop layer without changing the native render operation.

Keeping the native declarations in `PdfiumNative` makes the supported ABI surface reviewable and allows explicit
handling of platform differences such as PDFium `unsigned long`. PDFiumCore remains valuable as a comparison baseline
and as the better choice when an application needs the broader API.

## Reading The Benchmarks

`make benchmark-compare` runs two deliberately separate comparisons:

1. The hot-render comparison keeps documents, pages, and pinned BGRA buffers open. It verifies exact raw-pixel
   equality before timing and measures wrapper overhead around equivalent `FPDF_RenderPageBitmap` work.
2. The end-to-end comparison opens the tracked PDF, renders the first page at 144 DPI, encodes fast PNG through the
   same SkiaSharp settings, and closes all per-operation resources. It compares PdfiumRaster with PDFiumCore plus the
   application code needed to supply the same workflow.

Both comparisons pin PDFiumCore and the native runtime packages to one exact release. Results describe one machine,
runtime, document, and option set; they are not a universal speed ranking.

## Additive 2.x Safety Roadmap

The following items stay within PDF-to-image scope and preserve existing public behavior:

1. Add `PdfDocument.Open(string, ...)`, `Open(byte[], ...)`, and `Open(Stream, ...)` factories whose returned document
   owns a `PdfiumLibrary` reference. Keep the current `Load` factories for advanced callers and compatibility, then use
   the owning factories inside high-level workflows.
2. Add an opt-in `PdfRenderLimits` policy with nullable maximum width, height, and total pixel count. Validate the
   calculated dimensions before allocating a managed or native bitmap, and snapshot the policy when dispatcher work
   is submitted. Unspecified limits preserve current behavior.
3. Keep PDFiumCore and all native runtime packages on one central version property. For each update, verify generated
   binding/native parity, raw pixel equivalence, the platform smoke matrix, `make test`, `make pack`, and the comparison
   benchmarks.

This roadmap does not expand PdfiumRaster into text extraction, PDF editing, form filling, signing, or a viewer.
