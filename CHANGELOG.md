# Changelog

All notable changes to this project will be documented in this file.

This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) and uses
the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.

---

## [1.4.0] – 2026-01-03

### Added

- **Advanced Short Heading Settings dialog**
    - Configurable maximum heading length (range 3–30, default 8).
    - Fine-grained pattern controls:
        - All CJK characters
        - All ASCII characters
        - ASCII digits only (automatically enabled when ASCII is selected)
        - Mixed CJK + ASCII
    - Clear visual hierarchy with parent/child options, inspired by Visual Studio feature selection.

- **User-configurable short heading detection**
    - Introduces an advanced, regex-based override mechanism for heading detection.
    - Custom title patterns are evaluated **immediately after built-in title detection**
      and before other reflow heuristics.
    - Enables precise identification of structured book headings, including:
        - Front-matter titles (e.g. *序章*, *前言*, *楔子*)
        - Chapter banners (e.g. `第十二章 夜雨初歇`)
        - Decorated or stylized headings commonly found in novels and scanned PDFs
    - Supports unlimited pattern combinations via regular expressions
      (e.g. `xxx|yyy|zzz`), allowing full adaptation to diverse content styles.

- **Design-time preview support**
    - Added `Design.DataContext` for the Short Heading dialog,
      improving layout iteration and visual consistency during development.

- **Ignore untrusted PDF text (Pdfium)**
    - Added an option to skip repeated overlay- or annotation-like text during PDF extraction.
    - Uses object-level text extraction to reduce duplicated or non-content text in certain PDFs.
    - Intended as a rescue option for PDFs with visible duplicated headings, watermarks, or overlay noise.
    - Extraction-only filtering; does not modify the original PDF.
    - Available via the PDF context menu and persisted under `pdfOptions.ignoreUntrustedPdfText`.

- **DOCX (.docx) plain-text import support**
    - Extracts human-readable text from Microsoft Word documents into the source editor.
    - Handles paragraphs, numbered and bulleted lists, tables (flattened as TSV),
      headers/footers, footnotes, and comments.
    - Formatting is intentionally stripped to produce clean, editable plain text
      suitable for reflow processing and OpenCC conversion.

- **ODT (.odt) plain-text import support**
    - Extracts text from OpenDocument Text files via `content.xml`.
    - Supports paragraphs, headings, lists, and tables.
    - Designed for lightweight, predictable text editing in the source editor.

- **EPUB (.epub) plain-text import support**
    - Extracts human-readable text from EPUB eBooks by parsing the package manifest (OPF) and spine order.
    - Supports both XHTML (`.xhtml`) and legacy HTML (`.html` / `.htm`) chapters, including older Calibre-generated EPUBs.
    - Ignores CSS and presentation-only markup; text is extracted based on semantic structure
      (paragraphs, headings, block elements, and line breaks).
    - Skips non-content sections such as scripts, styles, and navigation documents (ToC) by default.
    - Output is normalized into clean, reflow-friendly plain text suitable for further
      paragraph reflow and OpenCC conversion.

- **About dialog**
    - Added a dedicated About dialog displaying application version,
      engine information, and project homepage.

### Changed

- **Reflow engine refactored into `ReflowModel`**
    - Moved all CJK paragraph reflow logic out of PDF helpers.
    - Shared by PdfPig and Pdfium extraction pipelines.
    - Greatly improves maintainability, testability, and reuse across formats.
- **Short heading detection upgraded**
    - Uses `ShortHeadingSettings` instead of a single integer value.
    - ASCII-only headings automatically allow a larger effective length
      (`maxLen × 2`, clamped to 10–30) to better support English headings
      such as *Introduction*, *Chapter One*, *Black Water*, etc.
- **PDF reflow heuristics improved**
    - Better handling of dialog continuation, punctuation-based joins,
      metadata lines, and mixed CJK/ASCII content.
    - More robust collapse of layout-level repeated titles and headings.
    - Detect drawing box line pattern as paragraph separator.
- **Main text import pipeline unified**
    - Drag-and-drop and Open File now share the same document-loading logic.
    - DOCX, ODT, and plain text files are consistently routed through the same
      source editor update path.
- **Internal architecture cleanup**
    - Clear separation between:
        - PDF extraction (PdfPig / Pdfium)
        - Office document parsing (DOCX / ODT)
        - Text reflow logic (ReflowModel)
        - User configuration (LanguageSettings / ShortHeadingSettings)
- **Paragraph end detection and reflow logic significantly improved**
    - Main-body paragraph splitting now strictly follows **standard CJK sentence rules**
      (`。！？` with proper closer handling), prioritizing correctness over aggressiveness.
    - Ellipsis-based endings (`……`, OCR `"..."`) are supported as **weak paragraph boundaries**
      only when the line is predominantly CJK, preventing false splits in English or technical text.
    - Structural lines (e.g. bracket-wrapped titles, book lists, metadata-like lines, dates, signatures)
      are handled separately from sentence punctuation, avoiding interference with normal prose.
    - Robust handling of common OCR artifacts:
        - ASCII `.` and `:` may be conditionally interpreted as CJK punctuation
          **only in strongly CJK contexts**.
    - Dialog continuity is strictly preserved:
        - Paragraph splits are always blocked while quotes or brackets remain unclosed,
          ensuring multi-line dialog stays intact.
    - Overall reflow behavior is now closer to **human-edited Chinese text layout**,
      especially for novels, essays, and scanned PDFs.
- Update `OpenccNetLib` to v1.4.0
- Update `OpenccNetLibGui` runtimes to `.Net 10`

### Notes

- This release focuses on **correctness, configurability, and long-term maintainability**
  of text reflow and document import.
- DOCX and ODT are treated as **input formats only**; all content is converted to
  plain text before editing, reflow, or Opencc conversion.
- Existing behavior remains compatible; legacy `ShortHeadingMaxLen` is internally
  synchronized with the new settings model.
- The reflow engine is now suitable for reuse across
  PDF, Office documents, EPUB, CLI tools, batch processing, and automated testing.

---

## [1.3.2] - 2025-12-07

### Added

- **PDF import support** for the Source panel using both **Pdfium** (native) and **UglyToad.PdfPig** (managed) engines.
- **CJK-aware PDF text reflow pipeline**:
    - Merges wrapped lines intelligently.
    - Preserves chapter titles and headings.
    - Repairs cross-page word breaks (e.g., `面` + `容` → `面容`).
    - Handles CJK punctuation and spacing normalization.
- **Configurable PDF extraction options** (`LanguageSettings.json`):
    - `addPdfPageHeader` — insert or remove page markers (`=== [Page X/Y] ===`).
    - `compactPdfText` — enable compact reflow mode.
    - `autoReflowPdfText` — automatically reflow extracted PDF text.
    - `pdfEngine` — choose PdfPig or Pdfium.
    - `convertFilename` — convert filenames during batch operations.
- **Status-bar progressive feedback**:
    - Added fake progress bar with percentage indicator when loading multipage PDFs.
- **Drag-and-drop PDF loading**:
    - PDFs dragged into the Source editor now use the same extraction + reflow pipeline as the Open File dialog.
- **Selected-text reflow** for PDF text in AvaloniaEdit:
    - Supports forward & backward selections.
    - Reflows only the affected paragraph range.
- **PDF Options context menu**:
    - Toggle reflow, compact mode, page headers, and PDF engine directly from the UI.
- **PDF text extraction + Opencc conversion** in both **Main Conversion** and **Batch Conversion** modes.
- **Runtime PDF engine bindings included**:
    - `win-x64`, `win-x86`, `linux-x64`, `osx-x64`, `osx-arm64` native Pdfium binaries.

### Changed

- **Refined Fluent 2 UI theme**:
    - Improved Dark/Light mode contrast.
    - Enhanced editor pane borders and spacing.
    - Redesigned primary/secondary buttons using Fluent styling.
- **Batch mode no longer blocks the UI thread**:
    - All conversions (text + PDF) now run on background tasks (`Task.Run`).
    - Log entries update progressively instead of appearing in a single batch.
- **Improved AvaloniaEdit selection syncing**:
    - Fixed backward-selection offset issues.
    - Added precise selection restore after reflow.
- **Unified file dialogs and drag-and-drop behaviors** to match Fluent interaction patterns.

### Fixed

- Corrected missing newline behavior after selected-region reflow.
- Fixed incorrect selection shifting after reflow when user selected text backwards.
- Fixed Linux/macOS runtime loading failures by placing Pdfium binaries in correct RID folders.
- Eliminated duplicate PDF header lines in PdfPig extraction under certain layouts.

### Notes for .NET Runtimes

> This release (**v1.3.2**) will be the final version targeting **.NET 8**.  
> Beginning with the next major release (**v1.4.0**), `OpenccNetLibGui` will migrate to **.NET 10**  
> to take advantage of the improved **JIT performance**, **reduced memory usage**, updated libraries,  
> and **long-term ecosystem** support introduced in **.NET 10**.

> Existing users on **.NET 8** may continue using **v1.3.x** without issues.  
> However, new features, optimizations (including **PDF engine improvements**),  
> and future maintenance will be available only on the **.NET 10** builds

---

## [1.3.1] - 2025-11-20

### Added

- **New byte[]-based Office document conversion pipeline**  
  `OfficeDocModel.ConvertOfficeBytesAsync()` now provides a fully in-memory  
  **byte[] → byte[]** API for `.docx`, `.xlsx`, `.pptx`, `.odt`, `.ods`, `.odp`, and `.epub`.  
  This enables:
    - Future **Blazor / JS interop** (no file I/O required)
    - Safer sandbox execution (WASM, iOS, restricted environments)
    - Faster GUI integration without temporary disk access by callers  
      (internal extraction still uses a temp directory for now; will transition to ZipArchive-in-memory in a later
      version)

- **Optional file-based wrapper maintained**  
  `ConvertOfficeDocAsync(inputPath, outputPath, ...)` now internally delegates to  
  the new byte[] pipeline, ensuring GUI and CLI behavior remain identical.

### Changed

- Updated **OpenccNetLib** to `v1.3.1`
- Refactored **OfficeDocModel** to a clean architecture:
    - Core logic now operates entirely on in-memory containers (byte-in / byte-out)
    - File I/O is isolated to a thin wrapper layer only
    - Internal XML/EPUB processing is unchanged and remains fully compatible

- Ensured **100% conversion accuracy** across all Office/EPUB formats  
  after restructuring the pipeline.  
  This refactor introduces **no breaking changes** for existing users.

### Notes

- **⚠️ Versioning Notice**  
  **OpenccNetLibGui v1.3.1 will be the final version targeting .NET 8.**  
  Starting from the next major release (**v1.4.0**),  
  **OpenccNetLibGui will move to .NET 10** to take advantage of:
    - performance improvements (Tiered PGO/EA/loop unrolling)
    - modern AOT optimizations
    - improved file/zip APIs
    - better WASM/Blazor integration

  `OpenccNetLib` will continue targeting **.NET Standard 2.0** to remain usable  
  across Windows, Linux, macOS, Unity, Xamarin, MAUI, Blazor, and older runtimes.

- This redesign prepares the ground for future enhancements:
    - Pure in-memory ZipArchive processing (no temporary directory)
    - Blazor WebAssembly support
    - Browser-side Office conversion via JS interop
    - Even faster GUI performance with fewer disk operations

- CLI behavior remains unchanged; file I/O continues to behave exactly as before.

---

## [1.3.0] - 2025-10-21

### Added

- Added Custom Chinese Language in UI Settings (繁體界面 / 简体界面)

### Changed

- Update OpenccNetLib to v1.3.0
- Refactor preview box from TextBox to AvaloniaEdit

### Fixed

- Fixed ignore file preview and file remove if no item selected

---

## [1.2.0] 2025-10.01

### Added

- Add convert filename in batch conversion
- Add conversion for file with no extension (as plain text)

### Changed

- Separate office filetypes from text filetypes
-
    - Update `OpenccNetLib` to v1.2.0

### Fixed

- Fixed file-drop status display
- Fixed OutFolder onFocus
- Fixed file preview for file with no extension

---

## [1.1.0] - 2025-08-18

### Changed

- Update OpenccNetLib to v1.1.0

## [1.0.3] - 2025-07-29

### Added

- Add support for conversion of old Epub format (HTML)

### Changed

- Update to OpenccNetLib v1.0.3

---

## [1.0.2.1] - 2025-07-09

### Fixed

- Fixed GUI radio button no conversion for
  Hk2S [#2](https://github.com/laisuk/OpenccNetLibGui/issues/2#issuecomment-3051032619)

---

## [1.0.2] - 2025-07-09

### Changed

- Update OpenccNetLib to v1.0.2
- Some code optimizations

### Fixed

- Fixed program crash due to null value in input field text
  code. [#2](https://github.com/laisuk/OpenccNetLibGui/issues/2)

---

## [1.0.1] – 2025-06-25

### Added

- Added support for Office Documents (.docx, .xlsx, .pptx, .odt, .ods, .odp,
  .epub) [#1](https://github.com/laisuk/OpenccNetLibGui/issues/1#issue-3147388190)
- Added setting to use Custom Dictionary

### Changed

- Update OpenccNetLib to v1.0.1

### Fixed

- Fixed UI adaptation in Dark Theme. [#1](https://github.com/laisuk/OpenccNetLibGui/issues/1#issuecomment-2993268242)

---

## [1.0.0] – 2025-06-18

### Added

- Initial public release of OpenccNetLibGui
- Cross-platform Avalonia GUI
- Supports Simplified ↔ Traditional Chinese conversion
- Built using OpenccNetLib 1.0.0

---
