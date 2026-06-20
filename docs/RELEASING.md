# Releasing PdfiumRaster

This checklist is for publishing `PdfiumRaster` to NuGet.

## 1. Update Version

Update `VersionPrefix` in:

```text
src/PdfiumRaster/PdfiumRaster.csproj
```

Use semantic versioning. Suggested first release:

```xml
<VersionPrefix>0.1.0</VersionPrefix>
```

## 2. Run Tests

```bash
dotnet test PdfiumRaster.slnx
```

## 3. Pack Locally

```bash
dotnet pack src/PdfiumRaster/PdfiumRaster.csproj -c Release -o artifacts
```

Expected outputs:

```text
artifacts/PdfiumRaster.<version>.nupkg
artifacts/PdfiumRaster.<version>.snupkg
```

## 4. Inspect Package

Verify the `.nupkg` contains:

```text
lib/netstandard2.0/PdfiumRaster.dll
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

## 5. Smoke Test Local Package

```bash
tmpdir=$(mktemp -d)
dotnet new console -n PdfiumRasterSmoke -o "$tmpdir/PdfiumRasterSmoke" --framework net10.0
cd "$tmpdir/PdfiumRasterSmoke"
cat > NuGet.config <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="/path/to/repo/artifacts" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
XML

dotnet add package PdfiumRaster
```

Use a real PDF and run:

```csharp
using PdfiumRaster;

PdfImageConverter.SavePng("input.pdf", pageNumber: 1, "page.png");
```

The app should run without manually copying PDFium native binaries.

## 6. Publish

```bash
dotnet nuget push artifacts/PdfiumRaster.<version>.nupkg \
  --api-key <NUGET_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```

## 7. Verify Published Package

Create a fresh app and install from NuGet.org:

```bash
dotnet new console -n PdfiumRasterPublishedSmoke --framework net10.0
cd PdfiumRasterPublishedSmoke
dotnet add package PdfiumRaster
```

Render one PDF page to confirm native assets restore and load correctly.

## 8. Tag Release

```bash
git tag v<version>
git push origin v<version>
```
