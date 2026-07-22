SHELL := /bin/bash
SOLUTION := PdfiumRaster.slnx
PROJECT := src/PdfiumRaster/PdfiumRaster.csproj
CONFIGURATION := Release
ARTIFACTS_DIR := artifacts
PACKAGE_VERSION := 2.0.1
PACKAGE := $(ARTIFACTS_DIR)/PdfiumRaster.$(PACKAGE_VERSION).nupkg

.PHONY: help restore build test test-local pack inspect-package smoke-package benchmark benchmark-compare benchmark-session benchmark-encoding benchmark-dispatcher benchmark-bmp benchmark-pipeline benchmark-native-buffer benchmark-all-pages release-check clean

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
		'  make benchmark-bmp     Compare row-based and contiguous BMP output' \
		'  make benchmark-pipeline Compare sequential and pipelined document export' \
		'  make benchmark-native-buffer Compare managed and PDFium-owned save buffers' \
		'  make benchmark-all-pages PDF=<path> Measure every page with managed and native buffers' \
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
	dotnet run -c Release --project benchmarks/PdfiumRaster.Benchmarks/PdfiumRaster.Benchmarks.csproj -- --artifacts BenchmarkDotNet.Artifacts --filter '*PdfiumCore*ComparisonBenchmarks*'

benchmark-session:
	dotnet run -c Release --project benchmarks/PdfiumRaster.Benchmarks/PdfiumRaster.Benchmarks.csproj -- --artifacts BenchmarkDotNet.Artifacts --filter '*PdfRenderSessionBenchmarks*'

benchmark-encoding:
	dotnet run -c Release --project benchmarks/PdfiumRaster.Benchmarks/PdfiumRaster.Benchmarks.csproj -- --artifacts BenchmarkDotNet.Artifacts --filter '*PngEncodingBenchmarks*'

benchmark-dispatcher:
	dotnet run -c Release --project benchmarks/PdfiumRaster.Benchmarks/PdfiumRaster.Benchmarks.csproj -- --artifacts BenchmarkDotNet.Artifacts --filter '*PdfRenderDispatcher*'

benchmark-bmp:
	dotnet run -c Release --project benchmarks/PdfiumRaster.Benchmarks/PdfiumRaster.Benchmarks.csproj -- --artifacts BenchmarkDotNet.Artifacts --filter '*BmpWriterBenchmarks*'

benchmark-pipeline:
	dotnet run -c Release --project benchmarks/PdfiumRaster.Benchmarks/PdfiumRaster.Benchmarks.csproj -- --artifacts BenchmarkDotNet.Artifacts --filter '*PdfDocumentPipelineBenchmarks*'

benchmark-native-buffer:
	dotnet run -c Release --project benchmarks/PdfiumRaster.Benchmarks/PdfiumRaster.Benchmarks.csproj -- --artifacts BenchmarkDotNet.Artifacts --filter '*PdfNativeSaveBufferBenchmarks*'

benchmark-all-pages:
	@if [[ -z "$(PDF)" ]]; then echo 'Usage: make benchmark-all-pages PDF=<pdf-path>' >&2; exit 2; fi
	dotnet build benchmarks/PdfiumRaster.Benchmarks/PdfiumRaster.Benchmarks.csproj -c Release
	@printf '%s\n' 'mode,dpi,format,pages,elapsed_ms,encoded_bytes,total_pixel_bytes,max_page_buffer_bytes,managed_allocated_bytes,peak_working_set_delta_bytes,gen0_collections,gen1_collections,gen2_collections'
	@for dpi in 96 144 300; do \
		for format in Bmp Png Jpeg Webp; do \
			for mode in native managed; do \
				dotnet benchmarks/PdfiumRaster.Benchmarks/bin/Release/net10.0/PdfiumRaster.Benchmarks.dll \
					--all-pages-measure "$(PDF)" "$$mode" "$$dpi" "$$format"; \
			done; \
		done; \
	done

release-check: test pack inspect-package smoke-package

clean:
	dotnet clean $(SOLUTION)
	rm -rf $(ARTIFACTS_DIR) BenchmarkDotNet.Artifacts benchmarks/PdfiumRaster.Benchmarks/bin benchmarks/PdfiumRaster.Benchmarks/obj src/PdfiumRaster/bin src/PdfiumRaster/obj tests/PdfiumRaster.Tests/bin tests/PdfiumRaster.Tests/obj
