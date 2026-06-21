# AGENTS.md

Guidance for agents working in this repository.

## Project Scope

PdfiumRaster is a .NET Standard PDF-to-image library backed by PDFium. Keep the library focused on rendering PDF pages to images. Do not add unrelated PDF editing, text extraction, form filling, or viewer features unless explicitly requested.

## Repository Layout

- Library code lives in `src/PdfiumRaster`.
- Tests live in `tests/PdfiumRaster.Tests`.
- Documentation lives in `README.md`, `docs/`, and `samples/`.
- Use `Directory.Packages.props` for package versions.
- Keep generated outputs out of Git. `artifacts/`, `bin/`, `obj/`, IDE files, and `.DS_Store` are ignored.

## Coding Style

- Target `netstandard2.0` for the library.
- Keep nullable reference types enabled and maintain null/argument validation on public APIs.
- Add XML documentation comments for every public type, member, enum value, and overload.
- Keep XML comments useful for NuGet IntelliSense: mention page indexes vs page numbers, units such as pixels/PDF points/DPI, stream ownership, and memory behavior.
- Prefer simple, explicit APIs that match existing patterns in `PdfImageConverter`, `PdfDocument`, and `PdfPageRenderOptions`.
- Keep public APIs backward compatible once released. Add overloads instead of changing existing method semantics unless the change is explicitly requested.
- Use ASCII text unless an existing file already requires non-ASCII.

## Native PDFium Rules

- Native PDFium calls must remain centralized in `PdfiumNative`.
- PDFium is not thread-safe. Keep native calls serialized through the shared lock.
- When adding P/Invoke signatures, verify them against the PDFium headers included in the `bblanchon.PDFium.*` NuGet packages.
- Be careful with platform ABI differences. In particular, PDFium `unsigned long` is 32-bit on Windows and 64-bit on macOS/Linux.
- Keep callback delegates rooted for as long as PDFium can call them.
- Keep unmanaged structures passed to PDFium alive for the required native lifetime.

## Large File Behavior

- Prefer path-based APIs and seekable streams for large PDFs.
- Seekable streams should use PDFium custom file access and must not be copied into a single managed byte array.
- Non-seekable streams may be buffered because PDFium requires random access.
- Byte array and Base64 APIs necessarily hold the full PDF in memory; document that clearly when changing those paths.
- Remember that rendered page bitmaps can still be large. DPI, scale, width, height, and page dimensions directly affect memory usage.

## Tests

- Use a local ignored PDF under `tests/PdfiumRaster.Tests/TestAssets/` for PDF rendering coverage when needed.
- Add focused tests for new public behavior and native lifetime changes.
- Run tests through the Makefile:

```bash
make test
```

- Before considering release-related work complete, also run:

```bash
make pack
```

- If MSBuild fails in a sandbox with named pipe/socket permissions, rerun the same command with the required approval rather than changing project settings.

## Packaging

- Use central package management only; do not add package versions directly to project files.
- The NuGet package must include XML documentation.
- The NuGet package must include or transitively reference native PDFium and SkiaSharp runtime assets so consumers can run basic rendering code after installing the package.
- Keep `README.md` package-ready because it is included in the NuGet package.

## Documentation

- Update `README.md` for user-facing API or behavior changes.
- Update `docs/API.md` for public API changes.
- Update `docs/RELEASING.md` for release process changes.
- Keep examples short and runnable.

## Makefile

The root `Makefile` is the standard command surface:

```bash
make help
make restore
make build
make test
make pack
make inspect-package
make clean
make benchmark
```

Keep it listed in `PdfiumRaster.slnx` as a solution item so it is visible in IDEs.
