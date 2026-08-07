# Changelog

All notable changes to PdfiumRaster are documented in this file.

## Unreleased

### Changed

- CI now resolves the package version from the project and smoke-tests the package produced by the current build.
- Library compiler warnings are treated as errors.
- The test suite now includes property-based managed-input tests and an 80% line-coverage gate with retained Cobertura
  output in CI.

### Security

- Documented vulnerability acknowledgement and remediation targets and the requirement to isolate native PDF fuzzing
  from the normal test host.

## 2.0.3 - 2026-08-07

### Changed

- Optimized color-mode post-processing in `PdfImageConverter`: the black-and-white threshold passes now share a
  single branch-free `uint` span kernel, and the luminance (grayscale) passes were rewritten on spans with hoisted
  locals. Output is unchanged — behavior is pinned by differential and pinning tests, and BenchmarkDotNet benchmarks
  cover the black-and-white kernel.

## 2.0.2 - 2026-08-05

### Fixed

- `PdfRenderDispatcher` now disposes caller-provided input streams it owns when a request fails before queue
  admission. This covers pre-canceled requests, full-queue rejection, cancellation while waiting for capacity, and
  dispatcher shutdown races. Streams passed with `leaveOpen: true` remain open.

## 2.0.1 - 2026-07-22

### Changed

- Added a detailed PDFiumCore comparison, equivalent rendering benchmarks, raw-pixel verification, and an additive
  safety roadmap.
- Updated the centrally managed PDFium dependencies.

## 2.0.0 - 2026-07-20

### Changed

- Changed the library target from .NET Standard 2.0 to .NET Standard 2.1.
- Added PDFium-owned native bitmap buffers for save operations to avoid a full-page managed pixel allocation.
- Expanded package smoke tests, all-pages measurements, and documentation for native-buffer rendering.

## 1.0.0 - 2026-07-17

### Added

- Added the stable rendering API, seekable-stream random access, and non-seekable stream buffering.
- Added package publishing, CI smoke tests, architecture and API documentation, and rendering benchmarks.

## 0.4.0 - 2026-07-17

### Added

- Added the bounded batch save pipeline with focused tests and benchmarks.
- Added contribution and security policies.

## 0.3.0 - 2026-07-16

### Added

- Added `PdfRenderDispatcher` for bounded concurrent request submission and overlapping image encoding.

## 0.2.0 - 2026-06-30

### Added

- Added reusable document and render-session APIs, image encoding options, selected-page export, and expanded color
  conversion support.

No release listed above fixed a publicly known PdfiumRaster vulnerability with a CVE or similar identifier.
