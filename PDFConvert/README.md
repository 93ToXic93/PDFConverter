# PDFConverter
🚀 PDFConvert.Engine is a high-performance .NET library that converts PDF documents into self-contained HTML pages by rasterizing each page into an &lt;img> element.


## 🔧 What It Uses

- **[PDFium](https://pdfium.googlesource.com/pdfium/)** → for PDF rendering (opens and rasterizes PDF pages).
- **[SkiaSharp](https://github.com/mono/SkiaSharp)** → for image encoding (WebP, PNG, JPEG).
- **[WebMarkupMin](https://github.com/Taritsyn/WebMarkupMin)** → for HTML minification (smaller, optimized output).

---

## ✨ Features

- Convert PDFs into **single standalone HTML files**.
- Each page rendered as an `<img>` (WebP, PNG, or JPEG).
- **DPI scaling** → control sharpness and file size.
- **Quality control** (1–100) for WebP/JPEG output.
- **Grayscale mode** for minimal file size and clean monochrome output.
- **Lazy loading** and **async decoding** for browser performance.
- Built-in **HTML minification** (compact and production-ready).

---

## 🚀 Usage Example

```csharp
using PDFConvert.Engine;
using PDFConvert.Engine.Enums;

// Load PDF as bytes
byte[] pdfBytes = File.ReadAllBytes("sample.pdf");

// Convert to HTML
string html = PDFConverter.PdfToHtmlRaster(
    pdfBytes,
    dpi: 144,                  // resolution (default: 144)
    quality: 100,              // image quality (default: 100)
    imageFormat: ImageFormat.webp, // webp/png/jpeg
    grayscale: false           // grayscale rendering (default: false)
);

// Save result
File.WriteAllText("output.html", html);
