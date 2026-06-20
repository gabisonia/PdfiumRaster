using System.Runtime.InteropServices;
using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfiumNativeLoadingTests
{
    [Fact]
    public void Runtime_identifier_matches_current_platform()
    {
        var rid = typeof(PdfiumLibrary).Assembly
            .GetType("PdfiumRaster.PdfiumNative", throwOnError: true)!
            .GetMethod("GetRuntimeIdentifier",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, null);

        Assert.IsType<string>(rid);
        Assert.Contains(RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(), (string)rid);
    }
}