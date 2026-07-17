# Releasing PdfiumRaster

This checklist is for publishing `PdfiumRaster` to NuGet.

Run a release from a reviewed commit on the intended release branch with a clean working tree. Confirm that the
version has not already been published to NuGet.org; package versions are immutable.

This is a maintainer procedure. Contributor setup and pull-request checks are documented in
[CONTRIBUTING.md](../CONTRIBUTING.md).

## GitHub Setup

Create a NuGet.org Trusted Publishing policy for this repository.

The publish workflow uses the `nuget` GitHub Actions environment and requests `id-token: write` permission so NuGet can authenticate the workflow through OpenID Connect. No long-lived `NUGET_API_KEY` secret or repository variable is required when Trusted Publishing is configured.

Configure the GitHub `nuget` environment with required reviewers if you want an approval gate before publishing.

## Manual Publish Workflow

Publishing is handled by `.github/workflows/publish-nuget.yml` and is only run manually.

Open GitHub Actions, choose `Publish NuGet`, then `Run workflow`.

Inputs:

```text
channel: beta | stable
version: package version
nuget_user: NuGet.org profile name that owns the trusted publishing policy
```

The workflow defaults to the stable channel. Select beta explicitly only for a prerelease.

Stable releases require a version without a prerelease suffix:

```text
channel = stable
version = 1.0.0
```

Beta releases can use an explicit prerelease version:

```text
channel = beta
version = 1.0.0-beta.1
```

Or use a base version and let the workflow append the run number:

```text
channel = beta
version = 1.0.0
```

This resolves to:

```text
1.0.0-beta.<github-run-number>
```

The workflow restores, tests, packs, smoke-tests the package in a fresh app, uploads package artifacts, and publishes both `.nupkg` and `.snupkg` files to NuGet.org using Trusted Publishing.

## Local Release Checklist

### 1. Update Version

Update `VersionPrefix` in:

```text
src/PdfiumRaster/PdfiumRaster.csproj
```

Use semantic versioning, for example:

```xml
<VersionPrefix>1.0.0</VersionPrefix>
```

The manual workflow can override this with `/p:PackageVersion=<version>`, so updating `VersionPrefix` is mostly for source consistency.

Keep `PACKAGE_VERSION` in the root `Makefile` synchronized with `VersionPrefix`. The local `inspect-package` and
`smoke-package` targets use that value to locate and install the package:

```make
PACKAGE_VERSION := 1.0.0
```

Update user-facing documentation and release notes for public API, behavior, runtime dependency, and performance
changes before packing.

### 2. Run Release Checks

```bash
make clean
make release-check
```

This runs the normal test suite, creates the NuGet and symbol packages, inspects the package contents, installs the
local package into a fresh console app with an isolated NuGet cache, and renders through both the synchronous converter
and concurrent dispatcher to confirm the managed dependency graph and native assets load correctly.

Local-only tests use ignored assets such as `tests/PdfiumRaster.Tests/TestAssets/annotations.pdf` and are not part of `make release-check`. Run them separately when the local asset exists:

```bash
make test-local
```

Performance benchmarks are not part of the release gate, but can be run before larger rendering changes:

```bash
make benchmark
```

For rendering or encoding optimizations, also preserve the focused reports when reviewing regressions:

```bash
make benchmark-session
make benchmark-encoding
make benchmark-compare
make benchmark-dispatcher
```

### 3. Pack Locally

```bash
make pack
```

Expected outputs:

```text
artifacts/PdfiumRaster.<version>.nupkg
artifacts/PdfiumRaster.<version>.snupkg
```

### 4. Inspect Package

```bash
make inspect-package
```

Verify the `.nupkg` contains:

```text
lib/netstandard2.0/PdfiumRaster.dll
lib/netstandard2.0/PdfiumRaster.xml
README.md
```

Verify package dependencies include:

```text
bblanchon.PDFium.Linux
bblanchon.PDFium.macOS
bblanchon.PDFium.Win32
SkiaSharp
SkiaSharp.NativeAssets.Linux.NoDependencies
SkiaSharp.NativeAssets.macOS
SkiaSharp.NativeAssets.Win32
```

Confirm that `lib/netstandard2.0/PdfiumRaster.xml` contains documentation entries for every newly added public API and
that the package README examples match the packed version.

### 5. Smoke Test Local Package

```bash
make smoke-package
```

The smoke test installs the local package into a fresh console app and renders a tracked test PDF without manually copying PDFium native binaries.

### 6. Publish Manually From CLI

```bash
dotnet nuget push artifacts/PdfiumRaster.<version>.nupkg \
  --api-key <NUGET_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```

Prefer the GitHub Actions workflow for real releases. Use the CLI only when intentionally bypassing Actions.

### 7. Verify Published Package

Create a fresh app and install from NuGet.org:

```bash
dotnet new console -n PdfiumRasterPublishedSmoke --framework net10.0
cd PdfiumRasterPublishedSmoke
dotnet add package PdfiumRaster
```

Render one PDF page to confirm native assets restore and load correctly.

Test at least one runtime identifier for each operating system supported by the release when native dependency
versions change. CI currently exercises Linux; Windows and macOS verification should be recorded separately for a
native-runtime update.

### 8. Tag And Record The Release

```bash
git tag v<version>
git push origin v<version>
```

Create the tag from the exact commit that produced the published package, then attach or link the `.nupkg`, `.snupkg`,
test result, and relevant benchmark summaries in the GitHub release. Summarize user-visible changes and any native
dependency or platform-support changes.
