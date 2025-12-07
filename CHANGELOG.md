# Changelog

All notable changes to this project will be documented in this file.

This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) and uses
the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.

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
- **PDF text extraction + OpenCC conversion** in both **Main Conversion** and **Batch Conversion** modes.
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

### Notes for .NET Runtimes

> This release (v1.3.2) will be the final version targeting .NET 8.  
> Beginning with the next major release (v1.4.0), OpenccNetLibGui will migrate to .NET 10  
> to take advantage of the improved JIT performance, reduced memory usage, updated libraries,  
> and long-term ecosystem support introduced in .NET 10.

> Existing users on .NET 8 may continue using v1.3.x without issues.  
> However, new features, optimizations (including PDF engine improvements),  
> and future maintenance will be available only on the .NET 10 builds

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
