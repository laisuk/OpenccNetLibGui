using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenccNetLib;

namespace OpenccNetLibGui.Models;

public static class ConvertOfficeDocModel
{
    public static async Task<(bool Success, string Message)> ConvertOfficeDocAsync(
        string inputPath,
        string outputPath,
        string format,
        Opencc converter,
        bool punctuation)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"{format}_temp_" + Guid.NewGuid());

        try
        {
            ZipFile.ExtractToDirectory(inputPath, tempDir);

            List<string>? targetXmlPaths = format switch
            {
                "docx" => new List<string> { Path.Combine("word", "document.xml") },
                "xlsx" => new List<string> { Path.Combine("xl", "sharedStrings.xml") },
                "pptx" => Directory.Exists(Path.Combine(tempDir, "ppt"))
                    ? Directory.GetFiles(Path.Combine(tempDir, "ppt"), "*.xml", SearchOption.AllDirectories)
                        .Where(path => Path.GetFileName(path).StartsWith("slide") ||
                                       path.Contains("notesSlide") ||
                                       path.Contains("slideMaster") ||
                                       path.Contains("slideLayout") ||
                                       path.Contains("comment"))
                        .Select(path => Path.GetRelativePath(tempDir, path))
                        .ToList()
                    : new List<string>(),
                "odt" => new List<string> { "content.xml" },
                _ => null
            };

            if (targetXmlPaths == null || targetXmlPaths.Count == 0)
            {
                return (false, $"❌ Unsupported or invalid format: {format}");
            }

            int convertedCount = 0;
            foreach (var relativePath in targetXmlPaths)
            {
                var fullPath = Path.Combine(tempDir, relativePath);
                if (!File.Exists(fullPath)) continue;

                string xmlContent = await File.ReadAllTextAsync(fullPath, Encoding.UTF8);
                string convertedXml = converter.Convert(xmlContent, punctuation);
                await File.WriteAllTextAsync(fullPath, convertedXml, Encoding.UTF8);
                convertedCount++;
            }

            if (File.Exists(outputPath)) File.Delete(outputPath);
            ZipFile.CreateFromDirectory(tempDir, outputPath, CompressionLevel.Optimal, false);

            return (true, $"✅ Successfully converted {convertedCount} file(s) in {format} document.");
        }
        catch (Exception ex)
        {
            return (false, $"❌ Conversion failed: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}