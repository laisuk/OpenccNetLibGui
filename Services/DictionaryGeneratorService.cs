using System;
using System.Collections.Generic;
using System.IO;
using OpenccNetLib;

namespace OpenccNetLibGui.Services;

public enum DictionaryOutputFormat { Zstd, Cbor, Json }

public sealed record CustomDictionaryRequest(DictSlot Slot, CustomDictMode Mode, string Path);

public sealed record DictionaryGenerationRequest(
    string BaseDirectory,
    string OutputDirectory,
    IReadOnlyList<CustomDictionaryRequest> CustomDictionaries,
    DictionaryOutputFormat Format,
    bool ReadableUnicodeJson);

public interface IDictionaryGeneratorService
{
    string Generate(DictionaryGenerationRequest request);
}

public sealed class DictionaryGeneratorService : IDictionaryGeneratorService
{
    public string Generate(DictionaryGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var outputPath = Path.Combine(request.OutputDirectory, GetOutputFileName(request.Format));
        var temporaryPath = Path.Combine(request.OutputDirectory,
            $".{Path.GetFileNameWithoutExtension(outputPath)}.{Guid.NewGuid():N}.tmp{Path.GetExtension(outputPath)}");

        try
        {
            var dictionary = DictionaryLib.FromDicts(request.BaseDirectory);
            if (request.CustomDictionaries.Count > 0)
            {
                var specs = new CustomDictSpec[request.CustomDictionaries.Count];
                for (var index = 0; index < request.CustomDictionaries.Count; index++)
                {
                    var item = request.CustomDictionaries[index];
                    specs[index] = new CustomDictSpec
                    {
                        Slot = item.Slot,
                        Mode = item.Mode,
                        Paths = new[] { item.Path }
                    };
                }

                dictionary = DictionaryLib.WithCustomDicts(dictionary, specs);
            }

            switch (request.Format)
            {
                case DictionaryOutputFormat.Zstd:
                    DictionaryLib.SaveJsonCompressed(temporaryPath, dictionary);
                    break;
                case DictionaryOutputFormat.Cbor:
                    DictionaryLib.SaveCbor(temporaryPath, dictionary);
                    break;
                case DictionaryOutputFormat.Json:
                    if (request.ReadableUnicodeJson)
                        DictionaryLib.SerializeToJsonUnescaped(temporaryPath, dictionary);
                    else
                        DictionaryLib.SerializeToJson(temporaryPath, dictionary);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(request.Format), request.Format, null);
            }

            File.Move(temporaryPath, outputPath, true);
            return outputPath;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string GetOutputFileName(DictionaryOutputFormat format) => format switch
    {
        DictionaryOutputFormat.Zstd => "dictionary_maxlength.zstd",
        DictionaryOutputFormat.Cbor => "dictionary_maxlength.cbor",
        DictionaryOutputFormat.Json => "dictionary_maxlength.json",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };
}
