# Changelog

All notable changes to this project will be documented in this file.

This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) and uses
the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.

---

## [1.3.1] - 2025-11-14

### Changed

- Updated **OpenccNetLib** to `v1.3.1`
- Attached `LICENSE` in published app output
- Switched to Avalonia’s newer `FlushAsync()` API to ensure reliable clipboard persistence on Windows  
  (added in recent Avalonia releases; resolves user-reported cases where clipboard content was lost if the app closed immediately)

### Notes

- Microsoft Windows’ OLE clipboard behavior changes slightly across versions and updates.  
  Earlier documentation claiming that clipboard operations are "always flushed automatically" is not consistently accurate.  
  Real-world testing showed that `SetTextAsync()` alone may not persist clipboard data if the application exits immediately.  
  Using Avalonia’s `FlushAsync()` provides the same guarantee as `OleFlushClipboard()` while remaining fully managed and cross-platform safe.

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
