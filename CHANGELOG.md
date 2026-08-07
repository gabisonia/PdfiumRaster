# Changelog

All notable changes to PdfiumRaster are documented in this file.

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
