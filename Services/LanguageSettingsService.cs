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

    private LanguageSettings ReadLanguageSettingsFromJson(string filePath)
    {
        return JsonConvert.DeserializeObject<LanguageSettings>(File.ReadAllText(filePath))!;
    }
}