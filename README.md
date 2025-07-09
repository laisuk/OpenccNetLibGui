# OpenccNetLibGui

[![GitHub Release](https://img.shields.io/github/v/release/laisuk/OpenccNetLibGui?display_name=tag&sort=semver)](https://github.com/laisuk/OpenccNetLibGui/releases/latest)
![Release](https://github.com/laisuk/OpenccNetLibGui/actions/workflows/release.yml/badge.svg)

**OpenccNetLibGui** is a Chinese text conversion application built with Avalonia and the MVVM design pattern. It leverages the [OpenccNetLib](https://www.nuget.org/packages/OpenccNetLib) library to provide simplified and traditional Chinese conversion.

## 🚀 Download

Download the latest version of **OpenccNetLibGui** for your platform:

- 💻 **[Windows (win-x64)](https://github.com/laisuk/OpenccNetLibGui/releases/latest/download/OpenccNetLibGui-v1.0.2-win-x64.zip)**
- 🐧 **[Linux (linux-x64)](https://github.com/laisuk/OpenccNetLibGui/releases/latest/download/OpenccNetLibGui-v1.0.2-linux-x64.zip)**
- 🍎 **[macOS (osx-x64)](https://github.com/laisuk/OpenccNetLibGui/releases/latest/download/OpenccNetLibGui-v1.0.2-osx-x64.zip)**

> 📦 These are **framework-dependent builds**, targeting `.NET 8`.  
> You must have [.NET Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime) installed to run them.

## Features

- **Chinese Conversion**: Convert between simplified and traditional Chinese text.
- **Single/Batch Conversion**: Perform Chinese text conversion in single or batch mode.
- Designed to convert most **text based file types** and **Office documents** (`.docx`, `.xlsx`, `.pptx`, `.odt`)

## Dependencies

- [Avalonia](https://avaloniaui.net/): Cross-platform .NET UI framework.
- [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit): Text editor for Avalonia with virtualization support.
- [OpenccNetLib](https://github.com/laisuk/OpenccNet): .NET library for conversions between Traditional and Simplified Chinese.
- [Newtonsoft.Json](https://www.newtonsoft.com/json): High-performance JSON framework for .NET.

## Getting Started

1. **Clone the repository**:
```bash
git clone https://github.com/laisuk/OpenccNetLibGui.git
```
2. **Navigate to the project directory**:
```bash
cd OpenccNetLibGui
```
3. **Restore dependencies**:
```bash
dotnet restore
```
4. **Build the project**:
```bash
dotnet build
```
5. **Run the application**:
```bash
dotnet run
```
## Usage

### Single Mode

![image01](./Assets/image01.png)  

Support most **text base** file types.
1. Paste the text or open a file you wish to convert (file/text drag and drop are supported on Windows and macOS).
2. Select the desired conversion configuration (e.g., Simplified to Traditional).
3. Click the **Process** button to see the results.

---

### Batch Mode

![image02](./Assets/image02.png)
![image03](./Assets/image03.png)  

Support most **text base** file types and **Office documents** (`.docx`, `.xlsx`, `.pptx`, `.odt`)

1. Select or drag file(s) into the source list box (File(s), drag and drop currently only supported on Windows and macOS).
2. Select the desired conversion configuration.
3. Set the output folder.
4. Click the **Batch Start** button to begin batch conversion.

### Custom Dictionary

Usage of custom dictionary can be set in `LanguageSettings.json`:

```json
{
  "dictionary": "dicts"
}
```

Options are:
1. `"dicts"` - _*.txt_ in directory `dicts`
2. `"json"` - _dictionary_maxlength.json_ 
3. `"cbor"` - _dictionary_maxlength.cbor_
4. None of above, default to `"zstd"` - _dictionary_maxlength.zstd_

## Contributing

Contributions are welcome! Please fork the repository and submit a pull request for any enhancements or bug fixes.

## License

This project is licensed under the MIT License. See the [LICENSE](./LICENSE) file for details.

## Acknowledgements

- [OpenCC](https://github.com/BYVoid/OpenCC) for the Chinese text conversion lexicon.
- [OpenccNet](https://github.com/laisuk/OpenccNet) for the .NET Chinese conversion library.
- [Avalonia](https://avaloniaui.net/) for the cross-platform UI framework.
- [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) for the text editor with virtualization.
- [Newtonsoft.Json](https://www.newtonsoft.com/json) for JSON parsing.
