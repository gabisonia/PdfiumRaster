SOLUTION := PdfiumRaster.slnx
PROJECT := src/PdfiumRaster/PdfiumRaster.csproj
CONFIGURATION := Release
ARTIFACTS_DIR := artifacts
PACKAGE_VERSION := 0.1.0
PACKAGE := $(ARTIFACTS_DIR)/PdfiumRaster.$(PACKAGE_VERSION).nupkg

.PHONY: help restore build test pack inspect-package clean

help:
	@printf '%s\n' \
		'Available targets:' \
		'  make restore          Restore NuGet packages' \
		'  make build            Build the solution in Release mode' \
		'  make test             Run the test suite' \
		'  make pack             Create NuGet and symbol packages' \
		'  make inspect-package  List package contents and nuspec metadata' \
		'  make clean            Remove build and package outputs'

restore:
	dotnet restore $(SOLUTION)

build:
	dotnet build $(SOLUTION) -c $(CONFIGURATION) --no-restore

test:
	dotnet test $(SOLUTION)

pack:
	dotnet pack $(PROJECT) -c $(CONFIGURATION) -o $(ARTIFACTS_DIR)

inspect-package: $(PACKAGE)
	unzip -l $(PACKAGE)
	unzip -p $(PACKAGE) PdfiumRaster.nuspec

clean:
	dotnet clean $(SOLUTION)
	rm -rf $(ARTIFACTS_DIR) src/PdfiumRaster/bin src/PdfiumRaster/obj tests/PdfiumRaster.Tests/bin tests/PdfiumRaster.Tests/obj
