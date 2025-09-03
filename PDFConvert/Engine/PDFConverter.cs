using System.Runtime.InteropServices;
using System.Text;
using PDFConvert.Engine.Enums;
using SkiaSharp;
using WebMarkupMin.Core;
using static PDFConvert.Engine.ModelConstants;

namespace PDFConvert.Engine;

/// <summary>
/// Converts a PDF document to an HTML page by rasterizing each PDF page
/// into an <img> tag. Supports PNG/WebP/JPEG, DPI scaling and optional grayscale.
/// </summary>
/// <remarks>
/// Rendering is done via PDFium; encoding via SkiaSharp. Output HTML is minified.
/// </remarks>
public static class PDFConverter
{
    private static readonly object _pdfiumLock = new();

    private static int _initCount = 0;

    private static readonly HtmlMinifier HtmlMinifier = new HtmlMinifier(new HtmlMinificationSettings
    {
        // Remove indentation/newlines while preserving semantics.
        WhitespaceMinificationMode = WhitespaceMinificationMode.Aggressive,

        // Always keep quotes around attribute values (safest for data: URIs, srcset, etc.).
        AttributeQuotesRemovalMode = HtmlAttributeQuotesRemovalMode.KeepQuotes,

        // Do not drop optional closing tags; safer across browsers/parsers.
        RemoveOptionalEndTags = false,

        // Squash <style> contents.
        MinifyEmbeddedCssCode = true,

        // Leave <script> contents as-is to avoid breaking inline handlers or minifier edge cases.
        MinifyEmbeddedJsCode = false
    });

    /// <summary>
    /// Rasterizes the given PDF bytes and returns a single self-contained HTML document
    /// where each page is an <img>.
    /// </summary>
    /// <param name="pdfBytes">Raw PDF file content.</param>
    /// <param name="dpi">
    /// Target rasterization DPI. Higher values increase sharpness and file size. Default: 144.
    /// </param>
    /// <param name="quality">
    /// Encoder quality (1–100). For WebP lossless this controls effort; for PNG it’s ignored;
    /// for JPEG it controls compression level. Default: 100.
    /// </param>
    /// <param name="imageFormat">
    /// Output image format per page (webp/png/jpeg). WebP lossless is recommended for text.
    /// </param>
    /// <param name="grayscale">
    /// If true, renders pages in 8-bit grayscale (smaller output, no color).
    /// </param>
    /// <returns>A minified HTML string containing all pages as <img> elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pdfBytes"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the PDF cannot be opened (e.g., invalid or password-protected) or bitmap creation fails.
    /// </exception>
    public static string PdfToHtmlRaster(
        byte[] pdfBytes,
        int dpi = 144,
        int quality = 100,
        ImageFormat imageFormat = ImageFormat.webp,
        bool grayscale = false)
    {
        if (pdfBytes == null)
            throw new ArgumentNullException(nameof(pdfBytes));

        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset='utf-8'/>")
            .Append("<meta name='viewport' content='width=device-width,initial-scale=1'/>")
            .Append(
                "<style>body{margin:0;padding:0}img.page{display:block;max-width:100%;height:auto;margin:0 auto}</style>")
            .Append("</head><body>");

        GCHandle gch = default;
        IntPtr doc = IntPtr.Zero;
        int pixelFormat = grayscale ? Pdfium.FPDFBitmap_Gray : Pdfium.FPDFBitmap_BGRA;

        lock (_pdfiumLock)
        {
            EnsureInit_NoLock();
            try
            {
                gch = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
                doc = Pdfium.FPDF_LoadMemDocument(gch.AddrOfPinnedObject(), pdfBytes.Length, null);

                if (doc == IntPtr.Zero)
                    throw new InvalidOperationException(CanNotOpenErrorMessage);

                int pageCount = Pdfium.FPDF_GetPageCount(doc);

                for (int i = 0; i < pageCount; i++)
                {
                    IntPtr page = Pdfium.FPDF_LoadPage(doc, i);

                    if (page == IntPtr.Zero) continue;

                    double wPt = Pdfium.FPDF_GetPageWidth(page);
                    double hPt = Pdfium.FPDF_GetPageHeight(page);
                    int widthPx = Math.Max(1, (int)Math.Ceiling(wPt * dpi / 72.0));
                    int heightPx = Math.Max(1, (int)Math.Ceiling(hPt * dpi / 72.0));
                    IntPtr bmp = Pdfium.FPDFBitmap_CreateEx(widthPx, heightPx, pixelFormat, IntPtr.Zero, 0);

                    if (bmp == IntPtr.Zero)
                    {
                        Pdfium.FPDF_ClosePage(page);
                        throw new InvalidOperationException(CreateBitmapFailed);
                    }

                    try
                    {
                        int stride = Pdfium.FPDFBitmap_GetStride(bmp);

                        Pdfium.FPDFBitmap_FillRect(bmp, 0, 0, widthPx, heightPx, 0xFFFFFFFF);

                        Pdfium.FPDF_RenderPageBitmap(
                            bmp,
                            page,
                            left: 0,
                            top: 0,
                            width: widthPx,
                            height: heightPx,
                            rotate: 0,
                            flags: Pdfium.FPDF_LCD_TEXT | Pdfium.FPDF_ANNOT | (grayscale ? Pdfium.FPDF_GRAYSCALE : 0));

                        IntPtr buf = Pdfium.FPDFBitmap_GetBuffer(bmp);
                        byte[] imgBytes = EncodeImageFromPointer(buf, widthPx, heightPx, stride, quality, imageFormat,
                            grayscale);

                        string mime = imageFormat switch
                        {
                            ImageFormat.webp => WEBP,
                            ImageFormat.png => PNG,
                            ImageFormat.jpeg => JPEG,
                            _ => "application/octet-stream"
                        };

                        html.Append("<img class='page' loading='lazy' decoding='async' src='data:")
                            .Append(mime).Append(";base64,")
                            .Append(Convert.ToBase64String(imgBytes))
                            .Append("'/>");
                    }
                    finally
                    {
                        Pdfium.FPDFBitmap_Destroy(bmp);
                        Pdfium.FPDF_ClosePage(page);
                    }
                }
            }
            finally
            {
                if (doc != IntPtr.Zero) Pdfium.FPDF_CloseDocument(doc);
                if (gch.IsAllocated) gch.Free();
                Release_NoLock();
            }
        }

        html.Append("</body></html>");
        return MinifyHtml(html.ToString());
    }

    /// <summary>
    /// Represents the result of converting a single PDF page to an image.
    /// </summary>
    /// <param name="Bytes">
    /// Raw image bytes in the requested format (WebP, PNG, JPEG).
    /// </param>
    /// <param name="Mime">
    /// The MIME type of the encoded image (e.g., "image/webp").
    /// </param>
    /// <param name="SuggestedFileName">
    /// Suggested file name for saving the image, including extension.
    /// </param>
    public sealed record ImageResult(byte[] Bytes, string Mime, string SuggestedFileName);

    
    /// <summary>
    /// Rasterizes all pages of the given PDF document into images.
    /// </summary>
    /// <param name="pdfBytes">
    /// Raw PDF file content as a byte array.
    /// </param>
    /// <param name="dpi">
    /// Target resolution in DPI. Higher values increase sharpness and file size. Default: 144.
    /// </param>
    /// <param name="quality">
    /// Image encoder quality (1–100). Affects JPEG compression level; 
    /// controls effort for WebP lossless; ignored for PNG. Default: 100.
    /// </param>
    /// <param name="imageFormat">
    /// Output image format per page (WebP, PNG, or JPEG). WebP is recommended for text-heavy PDFs.
    /// </param>
    /// <param name="grayscale">
    /// If true, renders pages in 8-bit grayscale (smaller files, no color). Default: false.
    /// </param>
    /// <param name="baseName">
    /// Optional base name for generated image files. If omitted, "doc" is used.
    /// SuggestedFileName will be constructed as &lt;baseName&gt;_page-001.ext, etc.
    /// </param>
    /// <returns>
    /// A list of <see cref="ImageResult"/> objects, one per page,
    /// each containing the encoded image bytes, MIME type, and suggested file name.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="pdfBytes"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the PDF cannot be opened or a bitmap could not be created.
    /// </exception>
    public static List<ImageResult> PdfToImages(
        byte[] pdfBytes,
        int dpi = 144,
        int quality = 100,
        ImageFormat imageFormat = ImageFormat.webp,
        bool grayscale = false,
        string? baseName = null) 
    {
        if (pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes));

        var results = new List<ImageResult>();
        GCHandle gch = default;
        IntPtr doc = IntPtr.Zero;
        int pixelFormat = grayscale ? Pdfium.FPDFBitmap_Gray : Pdfium.FPDFBitmap_BGRA;

        string mime = imageFormat switch
        {
            ImageFormat.webp => WEBP,
            ImageFormat.png => PNG,
            ImageFormat.jpeg => JPEG,
            _ => "application/octet-stream"
        };

        lock (_pdfiumLock)
        {
            EnsureInit_NoLock();
            try
            {
                gch = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
                doc = Pdfium.FPDF_LoadMemDocument(gch.AddrOfPinnedObject(), pdfBytes.Length, null);
                if (doc == IntPtr.Zero) throw new InvalidOperationException(CanNotOpenErrorMessage);

                int pageCount = Pdfium.FPDF_GetPageCount(doc);
                for (int i = 0; i < pageCount; i++)
                {
                    IntPtr page = Pdfium.FPDF_LoadPage(doc, i);
                    if (page == IntPtr.Zero) continue;

                    double wPt = Pdfium.FPDF_GetPageWidth(page);
                    double hPt = Pdfium.FPDF_GetPageHeight(page);
                    int widthPx = Math.Max(1, (int)Math.Ceiling(wPt * dpi / 72.0));
                    int heightPx = Math.Max(1, (int)Math.Ceiling(hPt * dpi / 72.0));

                    IntPtr bmp = Pdfium.FPDFBitmap_CreateEx(widthPx, heightPx, pixelFormat, IntPtr.Zero, 0);
                    if (bmp == IntPtr.Zero)
                    {
                        Pdfium.FPDF_ClosePage(page);
                        throw new InvalidOperationException(CreateBitmapFailed);
                    }

                    try
                    {
                        int stride = Pdfium.FPDFBitmap_GetStride(bmp);
                        Pdfium.FPDFBitmap_FillRect(bmp, 0, 0, widthPx, heightPx, 0xFFFFFFFF);
                        Pdfium.FPDF_RenderPageBitmap(
                            bmp, page, 0, 0, widthPx, heightPx, 0,
                            Pdfium.FPDF_LCD_TEXT | Pdfium.FPDF_ANNOT | (grayscale ? Pdfium.FPDF_GRAYSCALE : 0));

                        IntPtr buf = Pdfium.FPDFBitmap_GetBuffer(bmp);
                        byte[] imgBytes = EncodeImageFromPointer(buf, widthPx, heightPx, stride, quality, imageFormat,
                            grayscale);

                        string ext = imageFormat switch
                        {
                            ImageFormat.webp => "webp",
                            ImageFormat.png => "png",
                            ImageFormat.jpeg => "jpg",
                            _ => "bin"
                        };

                        string name =
                            $"{(string.IsNullOrWhiteSpace(baseName) ? "doc" : baseName)}_page-{i + 1:D3}.{ext}";
                        results.Add(new ImageResult(imgBytes, mime, name));
                    }
                    finally
                    {
                        Pdfium.FPDFBitmap_Destroy(bmp);
                        Pdfium.FPDF_ClosePage(page);
                    }
                }
            }
            finally
            {
                if (doc != IntPtr.Zero) Pdfium.FPDF_CloseDocument(doc);
                if (gch.IsAllocated) gch.Free();
                Release_NoLock();
            }
        }

        return results;
    }


    private static void EnsureInit_NoLock()
    {
        if (_initCount++ == 0) Pdfium.FPDF_InitLibrary();
    }

    private static void Release_NoLock()
    {
        if (--_initCount == 0) Pdfium.FPDF_DestroyLibrary();
    }

    /// <summary>
    /// Minifies the given HTML. Returns original HTML on minification errors.
    /// </summary>
    private static string MinifyHtml(string html)
    {
        var result = HtmlMinifier.Minify(html, generateStatistics: false);
        return result.Errors.Count == 0 ? result.MinifiedContent : html;
    }

    /// <summary>
    /// Encodes a PDFium bitmap buffer to the requested image format.
    /// </summary>
    /// <remarks>
    /// Uses Gray8 for grayscale and BGRA for color. PNG/WebP are lossless; JPEG is lossy.
    /// PNG ignores the quality parameter with this overload.
    /// </remarks>
    private static byte[] EncodeImageFromPointer(IntPtr pixels, int width, int height, int stride, int quality,
        ImageFormat imageFormat, bool grayscale)
    {
        var info = grayscale
            ? new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque)
            : new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

        using var pixmap = new SKPixmap(info, pixels, stride);

        using var data = imageFormat switch
        {
            ImageFormat.webp => pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, quality)),
            ImageFormat.jpeg => pixmap.Encode(SKEncodedImageFormat.Jpeg, quality),
            ImageFormat.png => pixmap.Encode(SKEncodedImageFormat.Png, quality),
            _ => null
        };

        return data?.ToArray() ?? [];
    }
}