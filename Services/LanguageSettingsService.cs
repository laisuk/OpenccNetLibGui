using System.IO;
using Newtonsoft.Json;

namespace OpenccNetLibGui.Services;

public class LanguageSettingsService
{
    public LanguageSettingsService(string settingsFilePath)
    {
        LanguageSettings = ReadLanguageSettingsFromJson(settingsFilePath);
    }

    public LanguageSettings? LanguageSettings { get; private set; }

    private static LanguageSettings ReadLanguageSettingsFromJson(string filePath)
    {
        if (File.Exists(filePath))
        {
            return JsonConvert.DeserializeObject<LanguageSettings>(File.ReadAllText(filePath))!;
        }

        const string languageSettingsText = @"
            {
                ""languages"": [
                    {
                        ""id"": 0,
                        ""code"": ""non-zho"",
                        ""name"": ""Non-zho (其它)""
                    },
                    {
                        ""id"": 1,
                        ""code"": ""zh-Hant"",
                        ""name"": ""zh-Hant (繁体)""
                    },
                    {
                        ""id"": 2,
                        ""code"": ""zh-Hans"",
                        ""name"": ""zh-Hans (简体)""
                    }
                ],
                ""charCheck"": 50,
                ""punctuations"": {
                    ""\u201C"": ""\u300C"",
                    ""\u201D"": ""\u300D"",
                    ""\u2018"": ""\u300E"",
                    ""\u2019"": ""\u300F""
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
                ""textFileTypes"": [
	                "".docx"",
	                "".xlsx"",
                    "".pptx"",
                    "".odt"",
                    "".ods"",
                    "".odp"",
                    "".epub""
                ],
                ""dictionary"": ""zstd""
            }";

        File.WriteAllText(filePath, languageSettingsText);
        return JsonConvert.DeserializeObject<LanguageSettings>(languageSettingsText)!;
    }
}