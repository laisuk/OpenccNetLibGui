using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace OpenccNetLibGui.Services;

public class LanguageSettingsService
{
    private readonly string _defaultSettingsPath;
    private string _lastSavedSnapshot;

    public string UserSettingsPath { get; }

    public bool IsDirty =>
        CreateSnapshot(LanguageSettings) != _lastSavedSnapshot;

    public LanguageSettingsService(
        string defaultSettingsPath,
        string userSettingsPath)
    {
        _defaultSettingsPath = defaultSettingsPath;
        UserSettingsPath = userSettingsPath;

        LanguageSettings = ReadMergedLanguageSettings(
            _defaultSettingsPath,
            UserSettingsPath);

        _lastSavedSnapshot = CreateSnapshot(LanguageSettings);
    }

    /// <summary>
    /// The in-memory language settings used by the application.
    /// Never <c>null</c>; if the file is missing or corrupted, a default
    /// configuration will be created and written back to disk.
    /// </summary>
    public LanguageSettings LanguageSettings { get; private set; }

    /// <summary>
    /// Saves current settings to UserLanguageSettings.json
    /// (does NOT touch default LanguageSettings.json)
    /// </summary>
    public void Save()
    {
        var dir = Path.GetDirectoryName(UserSettingsPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonConvert.SerializeObject(
            LanguageSettings,
            Formatting.Indented
        );

        File.WriteAllText(
            UserSettingsPath,
            json,
            new UTF8Encoding(false) // 🔑 preserve CJK, no BOM
        );

        // 🔑 Reset dirty state
        _lastSavedSnapshot = CreateSnapshot(LanguageSettings);
    }

    // ------ Save Diff settings only------
    private static readonly string[] DiffRootPaths =
    {
        "pdfOptions",
        "sentenceBoundaryMode",
        "punctuation",
        "convertFilename",
        "dictionary",
        "locale",
        "charCheck"
    };

    private static readonly JsonSerializerSettings JsonSaveSettings =
        new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

    private static JObject Pick(JObject src, IEnumerable<string> rootPaths)
    {
        var dst = new JObject();
        foreach (var p in rootPaths)
        {
            if (src.TryGetValue(p, out var token))
                dst[p] = token.DeepClone();
        }

        return dst;
    }

    private static JToken? DiffToken(JToken? defTok, JToken? curTok)
    {
        if (curTok is null) return null;
        if (defTok is null) return curTok.DeepClone();
        if (JToken.DeepEquals(defTok, curTok)) return null;

        if (defTok is not JObject dObj || curTok is not JObject cObj) return curTok.DeepClone();
        var diff = new JObject();
        foreach (var prop in cObj.Properties())
        {
            var dChild = dObj[prop.Name];
            var cChild = prop.Value;

            var childDiff = DiffToken(dChild, cChild);
            if (childDiff != null)
                diff[prop.Name] = childDiff;
        }

        return diff.HasValues ? diff : null;

        // Arrays (and values): treat as atomic
    }

    public void SaveDiffOnly()
    {
        // default JSON (shipped)
        var defaultObj = JObject.Parse(File.ReadAllText(_defaultSettingsPath));

        // IMPORTANT: use the SAME serializer settings (camelCase) as in default JSON
        var serializer = JsonSerializer.Create(JsonSaveSettings);
        var currentObj = JObject.FromObject(LanguageSettings, serializer);

        // current effective settings in memory
        // var currentObj = JObject.FromObject(LanguageSettings);

        // Optional: drop meta "info" so it never pollutes user file
        if (currentObj["sentenceBoundaryMode"] is JObject sbm)
            sbm.Remove("info");

        var defaultPick = Pick(defaultObj, DiffRootPaths);
        var currentPick = Pick(currentObj, DiffRootPaths);

        var diff = (JObject?)DiffToken(defaultPick, currentPick) ?? new JObject();

        var dir = Path.GetDirectoryName(UserSettingsPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(UserSettingsPath, diff.ToString(Formatting.Indented), new UTF8Encoding(false));

        _lastSavedSnapshot = JsonConvert.SerializeObject(LanguageSettings, Formatting.None);
    }

    private static string CreateSnapshot(LanguageSettings settings)
    {
        return JsonConvert.SerializeObject(
            settings,
            Formatting.None
        );
    }

    /// <summary>
    /// Reloads settings from disk (default + user overrides).
    /// Restores the shipped default file if it is missing/corrupt.
    /// Never creates user files/folders.
    /// </summary>
    public void Reload()
    {
        // Re-merge user overrides onto defaults (no file writes)
        LanguageSettings = ReadMergedLanguageSettings(
            _defaultSettingsPath,
            UserSettingsPath
        );

        _lastSavedSnapshot = CreateSnapshot(LanguageSettings);
    }

    private static LanguageSettings ReadMergedLanguageSettings(
        string defaultPath,
        string userPath)
    {
        // 🔑 Ensure shipped default exists (restore if missing/corrupt)
        var defaultSettings = ReadOrCreateLanguageSettings(defaultPath);

        if (!File.Exists(userPath))
            return defaultSettings;

        try
        {
            var userJson = File.ReadAllText(userPath);
            var userSettings =
                JsonConvert.DeserializeObject<LanguageSettings>(userJson);

            if (userSettings is null)
                return defaultSettings;

            // 🔑 Merge user → default
            JsonConvert.PopulateObject(
                userJson,
                defaultSettings
            );

            return defaultSettings;
        }
        catch
        {
            // Corrupted user file → ignore
            return defaultSettings;
        }
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
        ""zh-Hant (TRAD)"",
        ""zh-Hans (SIMP)""
      ],
      ""t2sContent"": ""zh-Hant (TRAD) to zh-Hans (SIMP)"",
      ""s2tContent"": ""zh-Hans (SIMP) to zh-Hant (TRAD)"",
      ""customContent"": ""Custom Config"",
      ""stdContent"": ""General Region"",
      ""zhtwContent"": ""zh-CN / zh-TW Region"",
      ""hkContent"": ""zh-CN / zh-HK Region"",
      ""cbZhtwContent"": ""zh-CN / zh-TW Idioms"",
      ""cbPunctuationContent"": ""Punctuation"",
      ""customOptions"": [
        ""s2t"",
        ""s2tw"",
        ""s2twp"",
        ""s2hk"",
        ""t2s"",
        ""t2tw"",
        ""t2twp"",
        ""t2hk"",
        ""tw2s"",
        ""tw2sp"",
        ""tw2t"",
        ""tw2tp"",
        ""hk2s"",
        ""hk2t"",
        ""t2jp"",
        ""jp2t""
      ],
      ""batchLogContents"": {
        ""conversionType"": ""Conversion Type"",
        ""region"": ""Region"",
        ""zhtwIdioms"": ""ZH/TW Idioms"",
        ""punctuations"": ""Punctuations"",
        ""convertFilename"": ""Convert filename"",
        ""outputFolder"": ""Output folder""
      }
    },
    {
      ""id"": 1,
      ""code"": ""zh-Hant"",
      ""name"": [
        ""Non-zho (其它)"",
        ""zh-Hant (繁體)"",
        ""zh-Hans (簡體)""
      ],
      ""t2sContent"": ""zh-Hant (繁體) to zh-Hans (簡體)"",
      ""s2tContent"": ""zh-Hans (簡體) to zh-Hant (繁體)"",
      ""customContent"": ""Manual (自定義)"",
      ""stdContent"": ""General (通用簡繁)"",
      ""zhtwContent"": ""ZH-TW (中臺簡繁)"",
      ""hkContent"": ""ZH-HK (中港簡繁)"",
      ""zbZhtwContent"": ""ZH-TW Idioms (中臺慣用語)"",
      ""cbPunctuationContent"": ""Punctuation (標點)"",
      ""customOptions"": [
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
      ],
      ""batchLogContents"": {
        ""conversionType"": ""Conversion Type (轉換方式)"",
        ""region"": ""Region (區域)"",
        ""zhtwIdioms"": ""ZH/TW Idioms (中臺慣用語)"",
        ""punctuations"": ""Punctuations (標點)"",
        ""convertFilename"": ""Convert filename (轉換文件名)"",
        ""outputFolder"": ""Output folder (輸出文件夾)""
      }
    },
    {
      ""id"": 2,
      ""code"": ""zh-Hans"",
      ""name"": [
        ""Non-zho (其它)"",
        ""zh-Hant (繁体)"",
        ""zh-Hans (简体)""
      ],
      ""t2sContent"": ""zh-Hant (繁体) to zh-Hans (简体)"",
      ""s2tContent"": ""zh-Hans (简体) to zh-Hant (繁体)"",
      ""customContent"": ""Manual (自定义)"",
      ""stdContent"": ""General (通用简繁)"",
      ""zhtwContent"": ""ZH-TW (中台简繁)"",
      ""hkContent"": ""ZH-HK (中港简繁)"",
      ""cbZhtwContent"": ""ZH-TW Idioms (中台惯用语)"",
      ""cbPunctuationContent"": ""Punctuation (标点)"",
      ""customOptions"": [
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
      ],
      ""batchLogContents"": {
        ""conversionType"": ""Conversion Type (转换方式)"",
        ""region"": ""Region (区域)"",
        ""zhtwIdioms"": ""ZH/TW Idioms (中台惯用语)"",
        ""punctuations"": ""Punctuations (标点)"",
        ""convertFilename"": ""Convert filename (转换文件名)"",
        ""outputFolder"": ""Output folder (输出文件夹)""
      }
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
    ""ignoreUntrustedPdfText"": 0,
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
  ""sentenceBoundaryMode"": {
    ""info"": ""1 = lenient, 2 = balanced (default), 3 = strict"",
    ""value"": 2
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