# 📄 Pdfium Text Extraction Pipeline  
**ZhoConverterGui / OpenccNetLib – Models/PdfiumExtraction.md**

This document describes the internal and public APIs used by the Pdfium-based text extraction path.  
It explains how PDF pages are loaded, how text is extracted safely and efficiently, and why certain low-level techniques (such as unsafe buffer copying) are used.

---

# 1. 📘 Public API

## `ExtractText(string pdfPath)`
Synchronous extraction using PDFium.

```csharp
/// <summary>
/// Synchronously extracts plain text from a PDF file using PDFium.
/// </summary>
/// <param name="pdfPath">Full path to the PDF file to open.</param>
/// <returns>
/// The concatenated plain-text content of all pages in the PDF, with page
/// markers inserted in the form <c>=== [Page X/Y] ===</c>.
/// </returns>
/// <exception cref="ArgumentException">
/// Thrown when <paramref name="pdfPath"/> is invalid.
/// </exception>
/// <exception cref="InvalidOperationException">
/// Thrown when PDFium cannot load the document.
/// </exception>
/// <remarks>
/// Always adds page headers.  
/// Only text-embedded PDFs are supported (image scans require OCR).
/// </remarks>
```

---

## `ExtractTextAsync(...)`
Asynchronous PDFium extraction with progress callback.

```csharp
/// <summary>
/// Asynchronously extracts plain text from a PDF file using PDFium,
/// with optional page headers and progress reporting.
/// </summary>
/// <param name="pdfPath">PDF path.</param>
/// <param name="addPdfPageHeader">
/// If true, inserts <c>=== [Page X/Y] ===</c> markers.
/// </param>
/// <param name="statusCallback">
/// Optional progress reporting callback that receives strings like:
/// <c>"Loading PDF [#####-----] 45%"</c>.
/// </param>
/// <param name="cancellationToken">Cancellation support.</param>
/// <returns>A task returning extracted text.</returns>
/// <remarks>
/// Work is executed via:
/// <see cref="Task.Run{TResult}(Func{TResult}, CancellationToken)"/>  
/// Only text-embedded PDFs are supported.
/// </remarks>
```

---

# 2. 🔧 Internal Pipeline (`ExtractPages`)

```csharp
/// <summary>
/// Shared PDFium-based page loop used by both synchronous and asynchronous
/// extraction helpers.
/// </summary>
/// <param name="doc">An open PDFium document handle.</param>
/// <param name="addPdfPageHeader">Whether to prepend page headers.</param>
/// <param name="statusCallback">
/// Optional <c>(pageIndex, percent)</c> progress callback.
/// </param>
/// <param name="cancellationToken">Cancellation flag.</param>
/// <returns>Concatenated text across all pages.</returns>
/// <remarks>
/// Uses an adaptive progress interval based on page count.  
/// Reuses a shared UTF-16 buffer to minimize allocations.  
/// Does not close the document handle (caller owns it).
/// </remarks>
```

---

# 3. 📄 Per-Page Text Extraction (`ExtractPageText`)

```csharp
/// <summary>
/// Extracts raw UTF-16 text from a PDFium text page and returns it as a
/// managed <see cref="string"/>. Minimizes allocations by reusing a buffer.
/// </summary>
/// <param name="textPage">Handle from <c>FPDFText_LoadPage</c>.</param>
/// <param name="buffer">Reusable UTF-16 buffer.</param>
/// <returns>Extracted page text.</returns>
/// <remarks>
/// Removes trailing NUL appended by PDFium.  
/// Produces raw text only; reflow is handled by higher layers.
/// </remarks>
```

---

# 4. 🧬 UTF-16 → string helper (`Utf16BufferToString`)

```csharp
/// <summary>
/// Creates a managed <see cref="string"/> directly from a UTF-16 buffer
/// without using Encoding.Unicode.
/// </summary>
/// <param name="buffer">UTF-16 buffer.</param>
/// <param name="length">Number of code units to copy.</param>
/// <returns>Managed string.</returns>
/// <remarks>
/// Uses unsafe pointer ctor, but is fully bounds-checked and safe.  
/// Avoids redundant transcoding and reduces allocations.
/// </remarks>
```

---

# 5. 📚 PdfiumNative Functions Used

| API | Purpose |
|-----|---------|
| `FPDF_InitLibrary` / `FPDF_DestroyLibrary` | Library lifetime |
| `FPDF_LoadDocument` | Load PDF |
| `FPDF_GetPageCount` | Page count |
| `FPDF_LoadPage` / `FPDF_ClosePage` | Page lifetime |
| `FPDFText_LoadPage` / `FPDFText_ClosePage` | Text extraction |
| `FPDFText_CountChars` | UTF-16 char count |
| `FPDFText_GetText` | Extract text |

---

# 6. 🔄 Extraction Pipeline Flow

```
InitLibrary
LoadDocument
    ExtractPages
        LoadPage
        LoadTextPage
        ExtractPageText
        Utf16BufferToString
        CloseTextPage
        ClosePage
CloseDocument
DestroyLibrary
```

---

# 7. ⚠️ Limitations

- Only PDFs with embedded text produce meaningful output  
- OCR is not included  
- Layout is not preserved (plain text)

---

# 8. ✔ Summary

A fast, reliable, fully documented text extraction pipeline for PDFium:
- Zero transcoding  
- Minimal allocation  
- Clean UTF-16 handling  
- Safe, predictable flow  
- Fully integrated with progress UI

