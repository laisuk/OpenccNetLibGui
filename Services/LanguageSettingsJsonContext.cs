using System.Text.Json.Serialization;

namespace OpenccNetLibGui.Services;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(LanguageSettings))]
[JsonSerializable(typeof(Language))]
[JsonSerializable(typeof(CustomDictionarySetting))]
[JsonSerializable(typeof(PdfOptions))]
[JsonSerializable(typeof(ShortHeadingSettings))]
[JsonSerializable(typeof(SentenceBoundaryModeSetting))]
[JsonSerializable(typeof(RuntimeContents))]
[JsonSerializable(typeof(BatchLogContents))]
[JsonSerializable(typeof(DictionaryGeneratorContents))]
internal partial class LanguageSettingsJsonContext : JsonSerializerContext
{
}
