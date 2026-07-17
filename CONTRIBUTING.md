# Contributing to PdfiumRaster

Bug reports, documentation fixes, tests, and focused rendering improvements are welcome.

PdfiumRaster is deliberately a PDF-to-image library. Proposals for PDF editing, text extraction, form filling,
signing, or viewer UI are outside the project's scope.

## Report A Bug

Search the existing GitHub issues before opening a new one. Include enough information to reproduce the problem:

- PdfiumRaster version
- .NET runtime and operating system
- Runtime identifier and processor architecture
- Input type used (path, stream, byte array, or Base64)
- Render and encoding options
- Exception type, message, and stack trace
- A minimal PDF when it can be shared safely

Do not attach confidential PDFs. If a report may involve a security vulnerability, follow [SECURITY.md](SECURITY.md)
instead of opening a public issue.

## Propose A Change

Open an issue before investing in a large API or architectural change. Describe the use case and why the existing
API does not cover it. Small fixes can go directly to a pull request.

Public APIs must remain compatible after release. Prefer a new overload over changing the behavior of an existing
method.

## Build And Test

The .NET 10 SDK is required for the current test and benchmark projects. The library itself targets
`netstandard2.0`.

```bash
make restore
make build
make test
```

`make test` runs the tracked test suite. To test a private or large annotation-heavy document, place it at
`tests/PdfiumRaster.Tests/TestAssets/annotations.pdf`; that path is ignored by Git. Then run:

```bash
make test-local
```

Before submitting packaging or release changes, also run:

```bash
make pack
make inspect-package
```

If a change affects rendering speed or memory use, run the relevant target documented in
[docs/PERFORMANCE.md](docs/PERFORMANCE.md). Benchmark results should include the machine, operating system, .NET
version, input asset, and exact command.

## Code Guidelines

- Keep library code in `src/PdfiumRaster` and target `netstandard2.0`.
- Keep nullable reference types enabled and validate public arguments.
- Add useful XML documentation to every public type, member, enum value, and overload.
- State whether page values are zero-based indexes or 1-based numbers.
- State units such as pixels, PDF points, and DPI, plus stream ownership and material memory behavior.
- Keep all native PDFium calls in `PdfiumNative` and serialized through the shared lock.
- Root callback delegates and unmanaged structures for the complete native lifetime.
- Use central package versions in `Directory.Packages.props`.
- Update `README.md` and `docs/API.md` when public behavior changes.

For large files, preserve the existing behavior: paths and seekable streams use random access without a full managed
copy; buffering a non-seekable stream is acceptable because PDFium requires random access.

## Pull Requests

Keep pull requests focused. Explain the user-visible change, implementation tradeoffs, and how it was verified. Add
tests for new public behavior and native lifetime changes. Generated output, benchmark artifacts, IDE files, and local
test PDFs must not be committed.
