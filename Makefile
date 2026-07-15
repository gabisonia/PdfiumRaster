SHELL := /bin/bash
SOLUTION := PdfiumRaster.slnx
PROJECT := src/PdfiumRaster/PdfiumRaster.csproj
CONFIGURATION := Release
ARTIFACTS_DIR := artifacts
PACKAGE_VERSION := 0.3.0
PACKAGE := $(ARTIFACTS_DIR)/PdfiumRaster.$(PACKAGE_VERSION).nupkg

.PHONY: help restore build test test-local pack inspect-package smoke-package benchmark benchmark-compare benchmark-session benchmark-encoding benchmark-dispatcher release-check clean

help:
	@printf '%s\n' \
		'Available targets:' \
		'  make restore          Restore NuGet packages' \
		'  make build            Build the solution in Release mode' \
		'  make test             Run the test suite, excluding local-only tests' \
		'  make test-local       Run local-only tests that use ignored assets' \
		'  make pack             Create NuGet and symbol packages' \
		'  make inspect-package  List package contents and nuspec metadata' \
		'  make smoke-package    Install the local package in a fresh app and render a page' \
		'  make benchmark        Run BenchmarkDotNet performance benchmarks' \
		'  make benchmark-compare Compare PdfiumRaster with PDFiumCore' \
		'  make benchmark-session Compare legacy and reusable session rendering' \
		'  make benchmark-encoding Compare PNG compression levels' \
		'  make benchmark-dispatcher Compare sequential and concurrent dispatcher batches' \
		'  make release-check    Run tests, pack, inspect, and package smoke test' \
		'  make clean            Remove build and package outputs'

restore:
	dotnet restore $(SOLUTION)

build:
	dotnet build $(SOLUTION) -c $(CONFIGURATION) --no-restore

test:
	dotnet test $(SOLUTION) --filter "Category!=Local"

test-local:
	dotnet test $(SOLUTION) --filter "Category=Local"

pack:
	dotnet pack $(PROJECT) -c $(CONFIGURATION) -o $(ARTIFACTS_DIR)

inspect-package: $(PACKAGE)
	unzip -l $(PACKAGE)
	unzip -p $(PACKAGE) PdfiumRaster.nuspec

smoke-package: $(PACKAGE)
	set -euo pipefail; \
	repo="$$(pwd)"; \
	tmpdir="$$(mktemp -d)"; \
	dotnet new console -n PdfiumRasterSmoke -o "$$tmpdir/PdfiumRasterSmoke" --framework net10.0 >/dev/null; \
	cd "$$tmpdir/PdfiumRasterSmoke"; \
	printf '%s\n' \
		'<?xml version="1.0" encoding="utf-8"?>' \
		'<configuration>' \
		'  <packageSources>' \
		'    <clear />' \
		"    <add key=\"local\" value=\"$$repo/$(ARTIFACTS_DIR)\" />" \
		'    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />' \
		'  </packageSources>' \
		'  <config>' \
		'    <add key="globalPackagesFolder" value=".packages" />' \
		'  </config>' \
		'</configuration>' > NuGet.config; \
	dotnet add package PdfiumRaster --version $(PACKAGE_VERSION) >/dev/null; \
	cp "$$repo/tests/PdfiumRaster.Tests/TestAssets/axf-annotation-1.pdf" ./input.pdf; \
	printf '%s\n' \
		'using PdfiumRaster;' \
		'' \
		'PdfImageConverter.SavePng("input.pdf", pageNumber: 1, "page.png");' \
		'using var dispatcher = new PdfRenderDispatcher();' \
		'await dispatcher.SavePageAsync("input.pdf", pageIndex: 0, "page-async.png", new PdfImageConversionOptions' \
		'{' \
		'    Render = PdfPageRenderOptions.ScreenPreview,' \
		'    Format = PdfImageOutputFormat.Png,' \
		'    Encoding = PdfImageEncodingOptions.Fast,' \
		'});' \
		'await dispatcher.CompleteAsync();' \
		'' \
		'if (!File.Exists("page.png") || new FileInfo("page.png").Length == 0 ||' \
		'    !File.Exists("page-async.png") || new FileInfo("page-async.png").Length == 0)' \
		'{' \
		'    throw new InvalidOperationException("Smoke test did not generate both page images.");' \
		'}' > Program.cs; \
	dotnet run --configuration Release

benchmark:
	dotnet run -c Release --project benchmarks/PdfiumRaster.Benchmarks/PdfiumRaster.Benchmarks.csproj -- --artifacts BenchmarkDotNet.Artifacts --filter '*'

benchmark-compare:
	dotnet run -c Release --project benchmarks/PdfiumRaster.Benchmarks/PdfiumRaster.Benchmarks.csproj -- --artifacts BenchmarkDotNet.Artifacts --filter '*PdfiumCoreComparisonBenchmarks*'

benchmark-session:
	dotnet run -c Release --project benchmarks/PdfiumRaster.Benchmarks/PdfiumRaster.Benchmarks.csproj -- --artifacts BenchmarkDotNet.Artifacts --filter '*PdfRenderSessionBenchmarks*'

benchmark-encoding:
	dotnet run -c Release --project benchmarks/PdfiumRaster.Benchmarks/PdfiumRaster.Benchmarks.csproj -- --artifacts BenchmarkDotNet.Artifacts --filter '*PngEncodingBenchmarks*'

benchmark-dispatcher:
	dotnet run -c Release --project benchmarks/PdfiumRaster.Benchmarks/PdfiumRaster.Benchmarks.csproj -- --artifacts BenchmarkDotNet.Artifacts --filter '*PdfRenderDispatcher*'

release-check: test pack inspect-package smoke-package

clean:
	dotnet clean $(SOLUTION)
	rm -rf $(ARTIFACTS_DIR) BenchmarkDotNet.Artifacts benchmarks/PdfiumRaster.Benchmarks/bin benchmarks/PdfiumRaster.Benchmarks/obj src/PdfiumRaster/bin src/PdfiumRaster/obj tests/PdfiumRaster.Tests/bin tests/PdfiumRaster.Tests/obj
