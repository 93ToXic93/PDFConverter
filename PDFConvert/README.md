# PDFConverter

🚀 **PDFConvert.Engine** is a high-performance .NET library that converts PDF documents into self-contained HTML pages or individual images by rasterizing each page with precision and efficiency.

## 🔧 What It Uses

- **[PDFium](https://pdfium.googlesource.com/pdfium/)** → PDF rendering engine (opens and rasterizes PDF pages)
- **[SkiaSharp](https://github.com/mono/SkiaSharp)** → Cross-platform image encoding (WebP, PNG, JPEG)
- **[WebMarkupMin](https://github.com/Taritsyn/WebMarkupMin)** → HTML minification for optimized output

---

## ✨ Features

### 🌐 HTML Conversion
- Convert PDFs into **single standalone HTML files**
- Each page rendered as an `<img>` element with **data URIs**
- **Responsive design** with mobile viewport support
- **Lazy loading** and **async decoding** for optimal browser performance
- Built-in **HTML minification** (compact and production-ready)

### 🖼️ Image Extraction
- **Extract individual pages** as separate image files
- **Batch processing** with suggested file names
- Support for **WebP**, **PNG**, and **JPEG** formats

### ⚙️ Rendering Options
- **DPI scaling** → control sharpness and file size (default: 144 DPI)
- **Quality control** (1–100) for WebP/JPEG compression
- **Grayscale mode** for minimal file size and clean monochrome output
- **Anti-aliasing** with LCD text rendering for crisp text

---

## 🚀 Usage Examples

### Convert PDF to HTML

```csharp
using PDFConvert.Engine;
using PDFConvert.Engine.Enums;

// Load PDF as bytes
byte[] pdfBytes = File.ReadAllBytes("document.pdf");

// Convert to self-contained HTML
string html = PDFConverter.PdfToHtmlRaster(
    pdfBytes,
    dpi: 144,                      // Resolution (higher = sharper, larger file)
    quality: 100,                  // Image quality (1-100)
    imageFormat: ImageFormat.webp, // webp (recommended) | png | jpeg
    grayscale: false               // Enable grayscale rendering
);

// Save the result
File.WriteAllText("output.html", html);
```

### Extract PDF Pages as Images

```csharp
// Extract all pages as individual images
List<ImageResult> images = PDFConverter.PdfToImages(
    pdfBytes,
    dpi: 300,                      // High resolution for print quality
    quality: 90,                   // Slight compression for smaller files
    imageFormat: ImageFormat.jpeg, // JPEG for photos, WebP for text
    grayscale: true,               // Grayscale for documents
    baseName: "my_document"        // File prefix (optional)
);

// Save each page
foreach (var image in images)
{
    File.WriteAllBytes(image.SuggestedFileName, image.Bytes);
    Console.WriteLine($"Saved: {image.SuggestedFileName} ({image.Mime})");
}
```

### Advanced Configuration

```csharp
// High-quality document conversion
string html = PDFConverter.PdfToHtmlRaster(
    pdfBytes,
    dpi: 200,                      // Higher DPI for detailed documents
    quality: 95,                   // Near-lossless quality
    imageFormat: ImageFormat.webp, // WebP lossless compression
    grayscale: false
);

// Compact grayscale output for text documents
string compactHtml = PDFConverter.PdfToHtmlRaster(
    pdfBytes,
    dpi: 120,                      // Lower DPI for smaller files
    quality: 80,                   // Moderate compression
    imageFormat: ImageFormat.jpeg, // JPEG for smaller file size
    grayscale: true                // Grayscale for text documents
);
```

---

## 📊 Image Format Guide

| Format | Best For | Quality | File Size | Notes |
|--------|----------|---------|-----------|-------|
| **WebP** | Text documents, mixed content | Lossless | Small-Medium | Recommended for most use cases |
| **PNG** | Graphics with transparency | Lossless | Large | Quality parameter ignored |
| **JPEG** | Photo-heavy documents | Lossy | Small | Good compression, no transparency |

---

## 🔧 API Reference

### `PdfToHtmlRaster`

Converts a PDF to a self-contained HTML page with embedded images.

**Parameters:**
- `pdfBytes` (byte[]) - Raw PDF file content
- `dpi` (int) - Target DPI (default: 144)
- `quality` (int) - Image quality 1-100 (default: 100)
- `imageFormat` (ImageFormat) - Output format (default: WebP)
- `grayscale` (bool) - Render in grayscale (default: false)

**Returns:** Minified HTML string with embedded page images

### `PdfToImages`

Extracts all PDF pages as individual image files.

**Parameters:**
- `pdfBytes` (byte[]) - Raw PDF file content
- `dpi` (int) - Target DPI (default: 144)
- `quality` (int) - Image quality 1-100 (default: 100)
- `imageFormat` (ImageFormat) - Output format (default: WebP)
- `grayscale` (bool) - Render in grayscale (default: false)
- `baseName` (string?) - Base name for files (default: "doc")

**Returns:** List of `ImageResult` objects with bytes, MIME type, and suggested filename

---

## ⚡ Performance Tips

- **Use WebP** for best compression with high quality
- **Lower DPI** (96-144) for web display, **higher DPI** (200-300) for print
- **Enable grayscale** for text documents to reduce file size by ~60%
- **Adjust quality** based on content: 100 for text, 80-90 for mixed content

---

## 🛡️ Error Handling

The library throws specific exceptions for common issues:

- `ArgumentNullException` - When PDF bytes are null
- `InvalidOperationException` - When PDF cannot be opened (corrupted, password-protected, or invalid format)

```csharp
try
{
    string html = PDFConverter.PdfToHtmlRaster(pdfBytes);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"PDF processing failed: {ex.Message}");
    // Handle corrupted or password-protected PDFs
}
```

---

## 📋 Requirements

- **.NET 6.0+** (uses modern C# features and records)
- **PDFium native libraries** (platform-specific)
- **SkiaSharp** for cross-platform image processing
- **WebMarkupMin** for HTML optimization

---

## 🎯 Use Cases

- **Document Viewers** - Convert PDFs for web display
- **Report Generation** - Embed PDFs in web applications
- **Archive Systems** - Create searchable HTML versions
- **Mobile Apps** - Lightweight PDF viewing
- **Image Extraction** - Extract pages for further processing
- **Print Workflows** - High-DPI image generation