using System.Runtime.InteropServices;

namespace PdfiumRaster;

internal static partial class PdfiumNative
{
    private static readonly string SyncRoot = string.Intern("PdfiumRaster.PdfiumNative.SyncRoot");

    internal static void FPDF_InitLibrary()
    {
        lock (SyncRoot)
        {
            Imports.FPDF_InitLibrary();
        }
    }

    internal static void FPDF_DestroyLibrary()
    {
        lock (SyncRoot)
        {
            Imports.FPDF_DestroyLibrary();
        }
    }

    internal static IntPtr FPDF_LoadDocument(string filePath, string? password)
    {
        lock (SyncRoot)
        {
            return Imports.FPDF_LoadDocument(filePath, password);
        }
    }

    internal static IntPtr FPDF_LoadMemDocument(IntPtr data, int size, string? password)
    {
        lock (SyncRoot)
        {
            return Imports.FPDF_LoadMemDocument(data, size, password);
        }
    }

    internal static void FPDF_CloseDocument(IntPtr document)
    {
        lock (SyncRoot)
        {
            Imports.FPDF_CloseDocument(document);
        }
    }

    internal static int FPDF_GetPageCount(IntPtr document)
    {
        lock (SyncRoot)
        {
            return Imports.FPDF_GetPageCount(document);
        }
    }

    internal static IntPtr FPDF_LoadPage(IntPtr document, int pageIndex)
    {
        lock (SyncRoot)
        {
            return Imports.FPDF_LoadPage(document, pageIndex);
        }
    }

    internal static void FPDF_ClosePage(IntPtr page)
    {
        lock (SyncRoot)
        {
            Imports.FPDF_ClosePage(page);
        }
    }

    internal static float FPDF_GetPageWidthF(IntPtr page)
    {
        lock (SyncRoot)
        {
            return Imports.FPDF_GetPageWidthF(page);
        }
    }

    internal static float FPDF_GetPageHeightF(IntPtr page)
    {
        lock (SyncRoot)
        {
            return Imports.FPDF_GetPageHeightF(page);
        }
    }

    internal static IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr firstScan, int stride)
    {
        lock (SyncRoot)
        {
            return Imports.FPDFBitmap_CreateEx(width, height, format, firstScan, stride);
        }
    }

    internal static void FPDFBitmap_Destroy(IntPtr bitmap)
    {
        lock (SyncRoot)
        {
            Imports.FPDFBitmap_Destroy(bitmap);
        }
    }

    internal static void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color)
    {
        lock (SyncRoot)
        {
            Imports.FPDFBitmap_FillRect(bitmap, left, top, width, height, color);
        }
    }

    internal static void FPDF_RenderPageBitmap(
        IntPtr bitmap,
        IntPtr page,
        int startX,
        int startY,
        int sizeX,
        int sizeY,
        int rotate,
        PdfRenderFlags flags)
    {
        lock (SyncRoot)
        {
            Imports.FPDF_RenderPageBitmap(bitmap, page, startX, startY, sizeX, sizeY, rotate, flags);
        }
    }

    internal static uint FPDF_GetLastError()
    {
        lock (SyncRoot)
        {
            return Imports.FPDF_GetLastError();
        }
    }

    internal static string GetNativeLibraryFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "pdfium.dll";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "libpdfium.so";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "libpdfium.dylib";
        }

        throw new PlatformNotSupportedException("PDFium native loading is configured for Windows, Linux, and macOS.");
    }

    internal static string GetRuntimeIdentifier()
    {
        var architecture = GetArchitectureName(RuntimeInformation.ProcessArchitecture);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "win-" + architecture;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "linux-" + architecture;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "osx-" + architecture;
        }

        throw new PlatformNotSupportedException("PDFium native loading is configured for Windows, Linux, and macOS.");
    }

    private static string GetArchitectureName(Architecture architecture)
    {
        switch (architecture)
        {
            case Architecture.X64:
                return "x64";
            case Architecture.X86:
                return "x86";
            case Architecture.Arm64:
                return "arm64";
            case Architecture.Arm:
                return "arm";
            default:
                throw new PlatformNotSupportedException("Unsupported architecture '" + architecture + "'.");
        }
    }

    private static class Imports
    {
        private const string LibraryName = "pdfium";

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void FPDF_InitLibrary();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void FPDF_DestroyLibrary();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern IntPtr FPDF_LoadDocument(string filePath, string? password);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern IntPtr FPDF_LoadMemDocument(IntPtr data, int size, string? password);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void FPDF_CloseDocument(IntPtr document);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int FPDF_GetPageCount(IntPtr document);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr FPDF_LoadPage(IntPtr document, int pageIndex);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void FPDF_ClosePage(IntPtr page);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern float FPDF_GetPageWidthF(IntPtr page);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern float FPDF_GetPageHeightF(IntPtr page);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr firstScan,
            int stride);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void FPDFBitmap_Destroy(IntPtr bitmap);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height,
            uint color);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void FPDF_RenderPageBitmap(
            IntPtr bitmap,
            IntPtr page,
            int startX,
            int startY,
            int sizeX,
            int sizeY,
            int rotate,
            PdfRenderFlags flags);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint FPDF_GetLastError();
    }
}
