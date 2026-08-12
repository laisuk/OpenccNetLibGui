# Changelog

All notable changes to this project will be documented in this file.

This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) and uses
the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.

---

## [1.6.2] - Unreleased

### Added

- Added a theme-aware **Dictionary** tab for generating OpenccNet dictionary artifacts directly from base text
  dictionaries in ZSTD, CBOR, or JSON format.
- Added ordered custom dictionary rows with selectable active slots, Append/Override modes, file pickers, validation,
  and support for duplicate slots while preserving application order.
- Added responsive background generation with busy-state controls, progress/status feedback, clear error reporting, and
  safe sibling temporary-file replacement to protect existing output artifacts on failure.
- Added an optional **Readable Unicode JSON** mode that emits unescaped Unicode text while retaining standard escaped
  JSON generation as the default.
- Added a localized **Global Conversion Dictionary** selector to Settings for choosing the default Zstd, `dicts`, JSON,
  or CBOR provider. The selection is saved through `UserLanguageSettings.json` and takes effect after restarting the
  application.
- Added extended Unicode compatibility normalization for CJK text, covering Unicode compatibility ideographs, Kangxi
  radicals, selected CJK radical forms, compatibility punctuation, and known PDF text-extraction artifacts.
- Added a localized **Extend Unicode Compatibility for CJK text normalization** setting that optionally applies the
  extended Unicode normalization pass after the existing compatibility normalization.
- Added a localized **Normalize Unicode compatibility** PDF option for PdfPig extraction. The option is enabled only
  when PdfPig is selected, retains the user's preference when Pdfium is active, and is synchronized between Settings and
  the existing editor PDF context menus.
- Added PDF load status reporting for Unicode compatibility normalization so the `Unicode-Normalized` state is shown
  only when the normalization pass was actually applied.

### Changed

- Extracted a focused singleton `SettingsViewModel` from `MainWindowViewModel`.
- It now owns theme mode, UI scale, editor appearance, window dimensions, global dictionary selection, settings
  dirty/save state, and their localized labels and hints.
- `MainWindowViewModel` continues to handle application-wide language coordination, PDF behavior, short-heading
  workflows, and active dictionary-provider loading.
- Updated Settings and main-window appearance bindings to use `Settings.*` directly, removing the temporary root-level
  forwarding properties and property-change relay while retaining the window-size persistence compatibility method used
  by the existing close handler.
- Preserved startup dictionary fallback and diff-only persistence behavior during the settings extraction, including
  resetting failed custom providers to ZSTD, keeping runtime and configured dictionary state distinct, and routing
  root-owned persistent settings through the single `SettingsViewModel.IsSettingsDirty` state.
- Repainted the dark theme with a softer Fluent-inspired charcoal palette and distinct layered surfaces across the main
  window, editors, settings, ComboBox and numeric controls, About dialog, validation popups, and Short Heading Settings
  dialog. The light theme remains unchanged.
- Updated ReactiveUI from `23.2.28` to the System.Reactive-compatible `ReactiveUI.Reactive 24.0.0` distribution and
  migrated namespaces and application scheduler initialization to the v24 API.
- Centralized exception observation for all `ReactiveCommand`s so failures retain their command name and full stack
  trace, break immediately when a debugger is attached, and surface appropriate status feedback in the owning view
  model.
- Avoided redundant default-provider resets at startup; only the recognized `dicts`, `json`, and `cbor` values trigger
  custom dictionary loading, while missing or unrecognized values leave the built-in Zstd provider untouched.
- Centralized PdfPig Unicode compatibility normalization in the PDF view-model pipeline. PdfPig text is now processed as
  extraction → Unicode compatibility normalization → optional auto-reflow → display, allowing single-file and batch PDF
  workflows that use the shared pipeline to receive the same preprocessing behavior.
- Made PdfPig Unicode compatibility normalization configurable and persistent, with normalization enabled by default.
  Selecting Pdfium disables the option in the UI without clearing its stored value, while the PDF pipeline independently
  enforces the PdfPig-only runtime gate.
- Added an 85% UI scale option for lower-resolution displays and compact workstation layouts. Supported UI scale values
  are now centralized so selection, validation, settings loading, and future scale additions share a single source of
  truth.
- Refined settings dirty-state tracking to exclude auto-persisted UI scale and window geometry from the explicit-save
  snapshot. UI scale, window width, and window height continue to persist automatically through
  `UserLanguageSettings.json` without incorrectly marking Settings as having unsaved changes.
- Improved application startup performance by lazily creating the Settings and Dictionary tab contents on first
  selection instead of constructing their visual trees during `MainWindow` initialization. Each view is cached after
  first use and reused for subsequent tab switches.
- Replaced the duplicated temporary-directory Office/EPUB conversion pipeline with direct delegation to
  `OpenccNetLib.OfficeDocConverter`; supported-format validation now comes from the library, and conversion output log
  inherits the shared in-memory conversion and atomic file-publishing behavior.

### Fixed

- Changed plain-text output from the main Save As and batch conversion workflows to explicit UTF-8 **without BOM**
  (UTF-8 signature), preventing conversion from silently inserting `EF BB BF` at the beginning of output files. This is
  particularly important for converted dictionaries and other machine-readable text files, where an unexpected BOM can
  interfere with first-line comment markers such as `#` or `//`, alter the first dictionary key, or cause subtle parsing
  and lookup issues. Batch conversion otherwise preserves the text and line-ending behavior produced by the existing
  input and PDF extraction pipelines.
- Prevented unobserved `ReactiveCommand` exceptions from being rethrown on the UI thread and terminating the
  application, while making failures such as unexpected null view-model state substantially easier to diagnose.
- Corrected PdfPig Unicode compatibility preprocessing so known extraction artifacts are normalized before CJK reflow.
  Scalar-preserving mappings such as `⸺ → —` repair extracted punctuation without expanding a single extracted character
  into multiple characters or altering the multiplicity of repeated dash characters.

---

## [1.6.1] - 2026-07-15

### Changed

- Hardened GUI Office/EPUB conversion against corrupted package output.
- Added explicit null, empty, and whitespace input validation.
- Added ZIP path traversal protection during extraction.
- Made GUI file output atomic so failed conversions do not replace existing output files.
- Allow commas in title headings when they appear within the first 20 characters.
- Handle standalone dialog closer line in reflow finalizer
- Added source and destination editor panel action rows for quick text actions.
- Fixed Theme Mode selection by binding to stable `ThemeModeOption` objects.
- Added a Normalize Compatibility Ideographs source action button with localized hints and status messages.
- Added a DeTofu destination action button, DeTofu level setting, persisted `deTofuLevel`, and localized settings label.
- Added key-binding `Ctrl-G` (go to line number) for Editor Source.
- Added CJK `Dialog Quotes Fixer` and `Validator` features.
- Added 2 new Manual conversion API `T2Hkp()` and `Hk2Tp()`.
- Update PDF native runtimes to `PDFium 151.0.7920.0`.
- Update `OpenccNetLib` to `v1.6.1`.

---

## [1.6.0] - 2026-06-20

### Changed

- Update `OpenccNetLib` to v1.6.0
- Minor UI refinement.
- Added `s2hkp` and `hk2sp` conversion configs.

---

## [1.5.1] - 2026-05-25

### Changed

- Update OpenccNetLib to v1.5.1
- Stabilized context menu emoji font family fallback
- Improved Custom Dictionary handling
- Optimized UserSettingsPath code flows
- Added About label for i18n

---

## [1.5.0] - 2026-05-07

### Changed

- Update Avalonia to version 12
- Refined Fluent UI
- Refined UI i18n
- Updated `OpenccNetLib` to v1.5.0
- Optimized Reflow function

---

## [1.4.2] – 2026-04-08

### Added

- Added `HasUnclosedDialogQuote()`

### Changed

- Optimized `ReflowModel`
- Optimized Reflow for handling typo of dialog quote
- Optimized MS Word `Numbering Context` extracted as text
- Updated `OpenccNetLib` to v1.4.2

### Fixed

- Fixed XLSX conversion to also process worksheet inline strings (`t="inlineStr"`), preventing missed text conversion in
  hybrid workbooks that contain both `shared strings` and `inline strings`.

---

## [1.4.1] – 2026-01-30

### Changed

- Update `OpenccNetLib` to v1.4.1
- CJK text reflow optimized
- Code optimization

---

## [1.4.0] – 2026-01-07

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
    - Supports unlimited pattern combinations via regular expressions (e.g. `xxx|yyy|zzz`), allowing full adaptation to
      diverse content styles.

- **Design-time preview support**
    - Added `Design.DataContext` for the Short Heading dialog, improving layout iteration and visual consistency during
      development.

- **Ignore untrusted PDF text (Pdfium)**
    - Added an option to skip repeated overlay- or annotation-like text during PDF extraction.
    - Uses object-level text extraction to reduce duplicated or non-content text in certain PDFs.
    - Intended as a rescue option for PDFs with visible duplicated headings, watermarks, or overlay noise.
    - Extraction-only filtering; does not modify the original PDF.
    - Available via the PDF context menu and persisted under `pdfOptions.ignoreUntrustedPdfText`.

- **DOCX (.docx) plain-text import support**
    - Extracts human-readable text from Microsoft Word documents into the source editor.
    - Handles paragraphs, numbered and bulleted lists, tables (flattened as TSV), headers/footers, footnotes, and
      comments.
    - Formatting is intentionally stripped to produce clean, editable plain text suitable for reflow processing and
      OpenCC conversion.

- **ODT (.odt) plain-text import support**
    - Extracts text from OpenDocument Text files via `content.xml`.
    - Supports paragraphs, headings, lists, and tables.
    - Designed for lightweight, predictable text editing in the source editor.

- **EPUB (.epub) plain-text import support**
    - Extracts human-readable text from EPUB eBooks by parsing the package manifest (OPF) and spine order.
    - Supports both XHTML (`.xhtml`) and legacy HTML (`.html` / `.htm`) chapters, including older `Calibre`-generated
      EPUBs.
    - Ignores CSS and presentation-only markup; text is extracted based on semantic structure (paragraphs, headings,
      block elements, and line breaks).
    - Skips non-content sections such as scripts, styles, and navigation documents (ToC) by default.
    - Output is normalized into clean, reflow-friendly plain text suitable for further paragraph reflow and OpenCC
      conversion.

- **About dialog**
    - Added a dedicated About dialog displaying application version, engine information, and project homepage.

### Changed

- **Reflow engine refactored into `ReflowModel`**
    - Moved all CJK paragraph reflow logic out of PDF helpers.
    - Shared by PdfPig and Pdfium extraction pipelines.
    - Greatly improves maintainability, testability, and reuse across formats.
- **Short heading detection upgraded**
    - Uses `ShortHeadingSettings` instead of a single integer value.
    - ASCII-only headings automatically allow a larger effective length (`maxLen × 2`, clamped to 10–30) to better
      support English headings such as *Introduction*, *Chapter One*, *Black Water*, etc.
- **PDF reflow heuristics improved**
    - Better handling of dialog continuation, punctuation-based joins, metadata lines, and mixed CJK/ASCII content.
    - More robust collapse of layout-level repeated titles and headings.
    - Detect drawing box line pattern as paragraph separator.
- **Main text import pipeline unified**
    - Drag-and-drop and Open File now share the same document-loading logic.
    - DOCX, ODT, and plain text files are consistently routed through the same source editor update path.
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
        - ASCII `.` and `:` may be conditionally interpreted as CJK punctuation **only in strongly CJK contexts**.
    - Dialog continuity is strictly preserved:
        - Paragraph splits are always blocked while quotes or brackets remain unclosed, ensuring multi-line dialog stays
          intact.
    - Overall reflow behavior is now closer to **human-edited Chinese text layout**, especially for novels, essays, and
      scanned PDFs.
- Update `OpenccNetLib` to v1.4.0
- Update `OpenccNetLibGui` runtimes to `.Net 10`

### Notes

- This release focuses on **correctness, configurability, and long-term maintainability**
  of text reflow and document import.
- DOCX and ODT are treated as **input formats only**; all content is converted to plain text before editing, reflow, or
  Opencc conversion.
- Existing behavior remains compatible; legacy `ShortHeadingMaxLen` is internally synchronized with the new settings
  model.
- The reflow engine is now suitable for reuse across PDF, Office documents, EPUB, CLI tools, batch processing, and
  automated testing.

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
