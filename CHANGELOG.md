# Changelog

All notable changes to PdfiumRaster are documented in this file.

## 2.0.2 - 2026-08-05

### Fixed

- `PdfRenderDispatcher` now disposes caller-provided input streams it owns when a request fails before queue
  admission. This covers pre-canceled requests, full-queue rejection, cancellation while waiting for capacity, and
  dispatcher shutdown races. Streams passed with `leaveOpen: true` remain open.
