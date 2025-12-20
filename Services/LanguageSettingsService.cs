using System.IO;
using Newtonsoft.Json;

namespace OpenccNetLibGui.Services;

public class LanguageSettingsService
{
    private readonly string _settingsFilePath;

    public LanguageSettingsService(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
        LanguageSettings = ReadOrCreateLanguageSettings(settingsFilePath);
    }

    /// <summary>
    /// The in-memory language settings used by the application.
    /// Never <c>null</c>; if the file is missing or corrupted, a default
    /// configuration will be created and written back to disk.
    /// </summary>
    public LanguageSettings LanguageSettings { get; private set; }

    /// <summary>
    /// Persists the current <see cref="LanguageSettings"/> to the JSON file.
    /// You can call this from a Settings dialog when the user clicks OK.
    /// </summary>
    public void Save()
    {
        var json = JsonConvert.SerializeObject(LanguageSettings, Formatting.Indented);
        File.WriteAllText(_settingsFilePath, json);
    }

    /// <summary>
    /// Reloads settings from disk, falling back to defaults if the file
    /// is missing or invalid. Useful if you ever add a "Reload" action.
    /// </summary>
    public void Reload()
    {
        LanguageSettings = ReadOrCreateLanguageSettings(_settingsFilePath);
    }

    private static LanguageSettings ReadOrCreateLanguageSettings(string filePath)
    {
        // 1) Try load existing file
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var settings = JsonConvert.DeserializeObject<LanguageSettings>(json);
                if (settings is not null)
                {
                    return settings;
                }
            }
            catch
            {
                // corrupted / unreadable → fall through to default
            }
        }

        // 2) Fallback: write default JSON and return defaults
        const string languageSettingsText = @"
{
  ""languages"": [
    {
      ""id"": 0,
      ""code"": ""non-zho"",
      ""name"": [
        ""Non-zho (Others)"",
        ""zh-Hant (Traditional)"",
        ""zh-Hans (Simplified)""
      ]
    },
    {
      ""id"": 1,
      ""code"": ""zh-Hant"",
      ""name"": [
        ""Non-zho (其它)"",
        ""zh-Hant (繁體)"",
        ""zh-Hans (簡體)""
      ],
      ""T2SContent"": ""zh-Hant (繁體) to zh-Hans (簡體)"",
      ""S2TContent"": ""zh-Hans (簡體) to zh-Hant (繁體)"",
      ""CustomContent"": ""Manual (自定義)"",
      ""StdContent"": ""General (通用簡繁)"",
      ""ZhtwContent"": ""ZH-TW (中臺簡繁)"",
      ""HkContent"": ""ZH-HK (中港簡繁)"",
      ""CbZhtwContent"": ""ZH-TW Idioms (中臺慣用語)"",
      ""CbPunctuationContent"": ""Punctuation (標點)"",
      ""CustomOptions"": [
        ""s2t (簡→繁)"",
        ""s2tw (簡→繁臺)"",
        ""s2twp (簡→繁臺/慣)"",
        ""s2hk (簡→繁港)"",
        ""t2s (繁→簡)"",
        ""t2tw (繁→繁臺)"",
        ""t2twp (繁→繁臺/慣)"",
        ""t2hk (繁→繁港)"",
        ""tw2s (繁臺→簡)"",
        ""tw2sp (繁臺→簡/慣)"",
        ""tw2t (繁臺→繁)"",
        ""tw2tp (繁臺→繁/慣)"",
        ""hk2s (繁港→簡)"",
        ""hk2t (繁港→繁)"",
        ""t2jp (日舊→日新)"",
        ""jp2t (日新→日舊)""
      ]
    },
    {
      ""id"": 2,
      ""code"": ""zh-Hans"",
      ""name"": [
        ""Non-zho (其它)"",
        ""zh-Hant (繁体)"",
        ""zh-Hans (简体)""
      ],
      ""T2SContent"": ""zh-Hant (繁体) to zh-Hans (简体)"",
      ""S2tContent"": ""zh-Hans (简体) to zh-Hant (繁体)"",
      ""CustomContent"": ""Manual (自定义)"",
      ""StdContent"": ""General (通用简繁)"",
      ""ZhtwContent"": ""ZH-TW (中台简繁)"",
      ""HkContent"": ""ZH-HK (中港简繁)"",
      ""CbZhtwContent"": ""ZH-TW Idioms (中台惯用语)"",
      ""CbPunctuationContent"": ""Punctuation (标点)"",
      ""CustomOptions"": [
        ""s2t (简→繁)"",
        ""s2tw (简→繁台)"",
        ""s2twp (简→繁台/惯)"",
        ""s2hk (简→繁港)"",
        ""t2s (繁→简)"",
        ""t2tw (繁→繁台)"",
        ""t2twp (繁→繁台/惯)"",
        ""t2hk (繁→繁港)"",
        ""tw2s (繁台→简)"",
        ""tw2sp (繁台→简/惯)"",
        ""tw2t (繁台→繁)"",
        ""tw2tp (繁台→繁/惯)"",
        ""hk2s (繁港→简)"",
        ""hk2t (繁港→繁)"",
        ""t2jp (日旧→日新)"",
        ""jp2t (日新→日旧)""
      ]
    }
  ],
  ""charCheck"": 50,
  ""punctuationChars"": {
    ""“"": ""「"",
    ""”"": ""」"",
    ""‘"": ""『"",
    ""’"": ""』""
  },
  ""textFileTypes"": [
    "".txt"",
    "".srt"",
    "".vtt"",
    "".ass"",
    "".xml"",
    "".ttml2"",
    "".csv"",
    "".json"",
    "".html"",
    "".cs"",
    "".py"",
    "".java"",
    "".md"",
    "".js""
  ],
  ""officeFileTypes"": [
    "".docx"",
    "".xlsx"",
    "".pptx"",
    "".odt"",
    "".ods"",
    "".odp"",
    "".epub""
  ],
  ""pdfOptions"": {
    ""addPdfPageHeader"": 0,
    ""compactPdfText"": 0,
    ""autoReflowPdfText"": 1,
    ""pdfEngine"": 2,
    ""shortHeadingSettings"": {
      ""maxLen"": 8,
      ""allCjk"": 1,
      ""allAscii"": 1,
      ""allAsciiDigits"": 1,
      ""mixedCjkAscii"": 1,
      ""customTitleHeadingRegex"": """"
    }
  },
  ""punctuation"": 1,
  ""convertFilename"": 0,
  ""dictionary"": ""zstd"",
  ""locale"": 2
}
";

        File.WriteAllText(filePath, languageSettingsText);
        var defaultSettings = JsonConvert.DeserializeObject<LanguageSettings>(languageSettingsText)!;
        return defaultSettings;
    }
}