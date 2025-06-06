# OpenccNetLibGui

**OpenccNetLibGui** is a Chinese text conversion application built using Avalonia and the MVVM design pattern. It leverages the [OpenccNetLib](https://www.nuget.org/packages/OpenccNetLib) library to provide functionalities such as simplified and traditional Chinese conversion.

## Features

- **Chinese Conversion**: Convert between simplified and traditional Chinese text.
- **Single/Batch Conversion**: Perform Chinese text convertion in single or batch mode.

## Dependencies

- [Avalonia](https://avaloniaui.net/): Cross-platform .NET UI framework.
- [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit): A WPF-based text editor for Avalonia with virtualization text display support.
- [OpenccNetLib](https://github.com/laisuk/OpenccNet): .Net Open Chinese Convert library for conversions between Traditional and Simplified Chinese.
- [Newtonsoft.Json](https://www.newtonsoft.com/json): Popular high-performance JSON framework for .NET.

## Getting Started

1. **Clone the repository**:

   ```bash
   git clone https://github.com/laisuk/OpenccNetLibGui.git

2. **Navigate to the project directory**:

    ```bash
    cd OpenccNetLibGui

3. **Restore dependencies**:
    ```bash
   dotnet restore

4. **Build the project**:
    ```bash
    dotnet build

5. **Run the application**:
    ```bash
    dotnet run

# Usage

![image01](./Assets/image01.png)

1. **Chinese Conversion Single Mode**:
- Paste the text or open file you wish to convert (File/Text drag and drop are supported in Windows and MacOS).
- Select the desired conversion configuration (e.g., Simplified to Traditional).
- Click the "Process" button to see the results.

---

![image02](./Assets/image02.png)

![image03](./Assets/image03.png)

2. **Chinese Conversion Batch Mode**:
- Select or drag file(s) into source list-box.
- Select the desired conversion configuration.
- Set the output folder.
- Click the "Batch Start" button to start batch conversion.

# Contributing
Contributions are welcome! Please fork the repository and submit a pull request for any enhancements or bug fixes.

# License
This project is licensed under the MIT License. See the [LICENSE](./LICENSE) file for details.


# Acknowledgements

- [OpenCC](https://github.com/BYVoid/OpenCC) for Chinese text conversion Lexicon.
- [OpenccNet](https://github.com/laisuk/OpenccNet) for Chinese convertion .Net library.
- [Avalonia](https://avaloniaui.net/) for the cross-platform UI framework.
- [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) for the TextEditor with virtualization.
- [Newtonsoft.Json](https://www.newtonsoft.com/json) for JSON parsing.



 

 