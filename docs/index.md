# PdfiumRaster

PdfiumRaster is a .NET Standard 2.1 library for rendering PDF pages to BMP, PNG, JPEG, and WebP images. PDFium renders pages, and SkiaSharp encodes compressed images. It focuses on PDF-to-image conversion.

Install the [NuGet package](https://www.nuget.org/packages/PdfiumRaster), then follow the [getting started guide](getting-started.md). The [API guide](API.md) covers input, rendering, output, and resource ownership; the [examples](examples.md) show common workflows.

## Choose a rendering workflow

- Use `PdfImageConverter` for individual pages or document exports.
- Use `PdfRenderSession` to reuse a document and render buffer for repeated work.
- Use `PdfRenderDispatcher` to queue unrelated requests with bounded backpressure and overlap encoding and output.

PDFium calls are serialized within a process. For parallel rendering across isolated local worker processes, use the companion [PdfiumRaster.Orchestrator](https://gabisonia.dev/PdfiumRaster.Orchestrator/). It builds on PdfiumRaster and adds a bounded worker pool, crash isolation, hard request timeouts, and worker replacement. Workers have the caller's operating system identity and permissions; they are not a security sandbox. See [architecture](ARCHITECTURE.md) and [performance](PERFORMANCE.md) for details.

The [repository README](https://github.com/gabisonia/PdfiumRaster/blob/master/README.md) is the package overview, and the XML documentation included in the NuGet package is the member-level API reference.
