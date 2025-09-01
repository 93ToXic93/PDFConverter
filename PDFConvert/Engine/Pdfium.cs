using System.Runtime.InteropServices;

namespace PDFConvert.Engine;

/// <summary>
/// Minimal P/Invoke wrapper around the native PDFium library.
/// Manages library init/shutdown, document/page access, and bitmap-based rendering.
/// </summary>
/// <remarks>
/// Pair every Init with Destroy, every Load* with Close*, and Destroy created bitmaps to avoid leaks.
/// Units for page width/height are PDF points (1/72 inch).
/// </remarks>
internal static class Pdfium
{
    private const string LIB = "pdfium";

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FPDF_InitLibrary();

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FPDF_DestroyLibrary();

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr FPDF_LoadMemDocument(IntPtr data, int size, string? password);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FPDF_CloseDocument(IntPtr doc);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FPDF_GetPageCount(IntPtr doc);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr FPDF_LoadPage(IntPtr doc, int pageIndex);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FPDF_ClosePage(IntPtr page);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "FPDF_GetPageWidth")]
        public static extern double FPDF_GetPageWidth(IntPtr page);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "FPDF_GetPageHeight")]
        public static extern double FPDF_GetPageHeight(IntPtr page);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr firstScan,
            int stride);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FPDFBitmap_Destroy(IntPtr bitmap);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int left, int top, int width, int height, int rotate, int flags);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FPDFBitmap_GetStride(IntPtr bitmap);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);

        public const int FPDF_LCD_TEXT = 0x02;
        public const int FPDF_ANNOT = 0x01;
        public const int FPDF_GRAYSCALE = 0x08;
        public const int FPDFBitmap_Gray = 1;
        public const int FPDFBitmap_BGRA = 3; 
}