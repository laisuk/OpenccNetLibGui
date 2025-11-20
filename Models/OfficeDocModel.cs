using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OpenccNetLib;

namespace OpenccNetLibGui.Models;

/// <summary>
/// Provides functionality to convert Office document formats (.docx, .xlsx, .pptx, .odt, .ods, .odp, .epub)
/// using the OpenCC converter. The core API works on in-memory <c>byte[]</c> containers, with an optional
/// file-based wrapper for convenience.
/// </summary>
public static class OfficeDocModel
{
    // Supported Office file formats for Office documents conversion.
    private static readonly HashSet<string> OfficeFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "docx", "xlsx", "pptx", "odt", "ods", "odp", "epub"
    };

    /// <summary>
    /// Determines whether the given file format is a supported Office or EPUB document format.
    /// </summary>
    /// <param name="format">
    /// The file format string to validate (e.g., "docx", "xlsx", "epub").
    /// The comparison is case-insensitive.
    /// </param>
    /// <returns>
    /// <c>true</c> if the format is one of the supported values ("docx", "xlsx", "pptx", "odt", "ods", "odp", "epub"); 
    /// otherwise, <c>false</c>.
    /// </returns>
    public static bool IsValidOfficeFormat(string? format)
    {
        return !string.IsNullOrWhiteSpace(format) && OfficeFormats.Contains(format);
    }

    /// <summary>
    /// Core API: converts an Office/EPUB container represented as a <c>byte[]</c>.
    /// </summary>
    /// <param name="inputBytes">Raw contents of the Office/EPUB container.</param>
    /// <param name="format">
    /// Logical document format ("docx", "xlsx", "pptx", "odt", "ods", "odp", or "epub").
    /// Case-insensitive.
    /// </param>
    /// <param name="converter">The OpenCC converter instance used for conversion.</param>
    /// <param name="punctuation">Whether to convert punctuation during OpenCC transformation.</param>
    /// <param name="keepFont">If <c>true</c>, font names are preserved using placeholder markers during conversion.</param>
    /// <returns>
    /// A tuple indicating whether the conversion succeeded, a status message,
    /// and the converted container bytes (or <c>null</c> on failure).
    /// </returns>
    public static async Task<(bool Success, string Message, byte[]? OutputBytes)> ConvertOfficeBytesAsync(
        byte[] inputBytes,
        string format,
        Opencc converter,
        bool punctuation,
        bool keepFont = false)
    {
        if (inputBytes is null) throw new ArgumentNullException(nameof(inputBytes));
        if (converter is null) throw new ArgumentNullException(nameof(converter));

        if (!IsValidOfficeFormat(format))
        {
            return (false, $"❌ Unsupported or invalid format: {format}", null);
        }

        var normalizedFormat = format.ToLowerInvariant();

        // Create a temporary working directory for extracted contents
        var tempDir = Path.Combine(Path.GetTempPath(), $"{normalizedFormat}_temp_" + Guid.NewGuid());

        try
        {
            Directory.CreateDirectory(tempDir);

            // Extract the ZIP container from the input bytes into tempDir
            using (var ms = new MemoryStream(inputBytes, writable: false))
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.FullName))
                        continue;

                    var destPath = Path.Combine(tempDir, entry.FullName);

                    // Create directory structure
                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);

                    // Skip directory entries
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                        entry.FullName.EndsWith("\\", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    using var entryStream = entry.Open();
                    using var fileStream = File.Create(destPath);
                    entryStream.CopyTo(fileStream);
                }
            }

            // Identify target XML/XHTML files for each Office/EPUB format
            var targetXmlPaths = normalizedFormat switch
            {
                "docx" => new List<string> { Path.Combine("word", "document.xml") },

                "xlsx" => new List<string> { Path.Combine("xl", "sharedStrings.xml") },

                "pptx" => Directory.Exists(Path.Combine(tempDir, "ppt"))
                    ? Directory.GetFiles(Path.Combine(tempDir, "ppt"), "*.xml", SearchOption.AllDirectories)
                        .Where(path =>
                            Path.GetFileName(path).StartsWith("slide", StringComparison.OrdinalIgnoreCase) ||
                            path.Contains("notesSlide", StringComparison.OrdinalIgnoreCase) ||
                            path.Contains("slideMaster", StringComparison.OrdinalIgnoreCase) ||
                            path.Contains("slideLayout", StringComparison.OrdinalIgnoreCase) ||
                            path.Contains("comment", StringComparison.OrdinalIgnoreCase))
                        .Select(path => Path.GetRelativePath(tempDir, path))
                        .ToList()
                    : new List<string>(),

                // ODT family: all use "content.xml"
                "odt" or "ods" or "odp" => new List<string> { "content.xml" },

                "epub" => Directory.Exists(tempDir)
                    ? Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories)
                        .Where(f =>
                            f.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".opf", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".ncx", StringComparison.OrdinalIgnoreCase))
                        .Select(f => Path.GetRelativePath(tempDir, f))
                        .ToList()
                    : new List<string>(),

                _ => null
            };

            // Check for unsupported or missing format
            if (targetXmlPaths == null || targetXmlPaths.Count == 0)
            {
                return (false, $"❌ Unsupported or invalid format: {format}", null);
            }

            var convertedCount = 0;

            // Process each target XML file
            foreach (var relativePath in targetXmlPaths)
            {
                var fullPath = Path.Combine(tempDir, relativePath);
                if (!File.Exists(fullPath)) continue;

                var xmlContent = await File.ReadAllTextAsync(fullPath, Encoding.UTF8)
                    .ConfigureAwait(false);

                Dictionary<string, string> fontMap = new();

                // Pre-process: replace font names with unique markers if keepFont is enabled
                if (keepFont)
                {
                    var fontCounter = 0;
                    var pattern = normalizedFormat switch
                    {
                        "docx" => @"(w:eastAsia=""|w:ascii=""|w:hAnsi=""|w:cs="")(.*?)("")",
                        "xlsx" => @"(val="")(.*?)("")",
                        "pptx" => @"(typeface="")(.*?)("")",
                        // Handle odt, ods, odp
                        "odt" or "ods" or "odp" =>
                            @"((?:style:font-name(?:-asian|-complex)?|svg:font-family|style:name)=[""'])([^""']+)([""'])",
                        "epub" => @"(font-family\s*:\s*)([^;""']+)",
                        _ => null
                    };

                    if (pattern != null)
                    {
                        xmlContent = Regex.Replace(xmlContent, pattern, match =>
                        {
                            var originalFont = match.Groups[2].Value;
                            var marker = $"__F_O_N_T_{fontCounter++}__";
                            fontMap[marker] = originalFont;

                            return normalizedFormat switch
                            {
                                "epub" =>
                                    match.Groups[1].Value + marker,
                                _ =>
                                    match.Groups[1].Value + marker + match.Groups[3].Value
                            };
                        });
                    }
                }

                // Run OpenCC conversion on the XML content
                var convertedXml = converter.Convert(xmlContent, punctuation);

                // Post-process: restore font names from markers
                if (keepFont)
                {
                    foreach (var kvp in fontMap)
                    {
                        convertedXml = convertedXml.Replace(kvp.Key, kvp.Value);
                    }
                }

                // Overwrite the file with the converted content
                await File.WriteAllTextAsync(fullPath, convertedXml, Encoding.UTF8)
                    .ConfigureAwait(false);
                convertedCount++;
            }

            // Return if no valid XML fragments found
            if (convertedCount == 0)
            {
                return (false,
                    $"⚠️ No valid XML fragments were found for conversion. Is the format '{format}' correct?",
                    null);
            }

            // Create the new ZIP/EPUB archive in memory with the converted files
            byte[] resultBytes;

            if (normalizedFormat == "epub")
            {
                var (zipSuccess, zipMessage, epubBytes) = CreateEpubZipWithSpec(tempDir);
                if (!zipSuccess || epubBytes is null)
                {
                    return (false, zipMessage, null);
                }

                resultBytes = epubBytes;
            }
            else
            {
                resultBytes = CreateZipFromDirectory(tempDir);
            }

            return (true, $"✅ Successfully converted {convertedCount} fragment(s) in {format} document.", resultBytes);
        }
        catch (Exception ex)
        {
            return (false, $"❌ Conversion failed: {ex.Message}", null);
        }
        finally
        {
            // Clean up temp directory
            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }
    }

    /// <summary>
    /// Optional file-based wrapper: reads the input file, converts it via the byte-based
    /// pipeline, then writes the output file.
    /// </summary>
    /// <param name="inputPath">The full path to the input Office document (e.g., .docx).</param>
    /// <param name="outputPath">The desired full path to the converted output file.</param>
    /// <param name="format">The document format ("docx", "xlsx", "pptx", "odt", "ods", "odp", or "epub").</param>
    /// <param name="converter">The OpenCC converter instance used for conversion.</param>
    /// <param name="punctuation">Whether to convert punctuation during OpenCC transformation.</param>
    /// <param name="keepFont">If true, font names are preserved using placeholder markers during conversion.</param>
    /// <returns>A tuple indicating whether the conversion succeeded and a status message.</returns>
    public static async Task<(bool Success, string Message)> ConvertOfficeDocAsync(
        string inputPath,
        string outputPath,
        string format,
        Opencc converter,
        bool punctuation,
        bool keepFont = false)
    {
        if (inputPath is null) throw new ArgumentNullException(nameof(inputPath));
        if (outputPath is null) throw new ArgumentNullException(nameof(outputPath));

        if (!File.Exists(inputPath))
        {
            return (false, $"❌ Input file not found: {inputPath}");
        }

        var inputBytes = await File.ReadAllBytesAsync(inputPath).ConfigureAwait(false);

        var (success, message, outputBytes) = await ConvertOfficeBytesAsync(
                inputBytes, format, converter, punctuation, keepFont)
            .ConfigureAwait(false);

        if (!success || outputBytes is null)
        {
            return (success, message);
        }

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        await File.WriteAllBytesAsync(outputPath, outputBytes).ConfigureAwait(false);
        return (true, message);
    }

    /// <summary>
    /// Creates a ZIP archive in memory from the specified source directory.
    /// </summary>
    private static byte[] CreateZipFromDirectory(string sourceDir)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var entryPath = Path.GetRelativePath(sourceDir, file).Replace('\\', '/');
                var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(file);
                fileStream.CopyTo(entryStream);
            }
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Creates a valid EPUB-compliant ZIP archive in memory from the specified source directory.
    /// Ensures the <c>mimetype</c> file is the first entry and uncompressed,
    /// as required by the EPUB specification.
    /// </summary>
    /// <param name="sourceDir">The temporary directory containing EPUB unpacked contents.</param>
    /// <returns>
    /// Tuple indicating success, an informative message, and the resulting EPUB container bytes.
    /// </returns>
    private static (bool Success, string Message, byte[]? OutputBytes) CreateEpubZipWithSpec(string sourceDir)
    {
        var mimePath = Path.Combine(sourceDir, "mimetype");

        try
        {
            if (!File.Exists(mimePath))
            {
                return (false, "❌ 'mimetype' file is missing. EPUB requires this as the first entry.", null);
            }

            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                // 1. Add mimetype first, uncompressed
                var mimeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
                using (var entryStream = mimeEntry.Open())
                using (var fileStream = File.OpenRead(mimePath))
                {
                    fileStream.CopyTo(entryStream);
                }

                // 2. Add the rest (recursively)
                foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    if (Path.GetFullPath(file) == Path.GetFullPath(mimePath))
                        continue;

                    var entryPath = Path.GetRelativePath(sourceDir, file).Replace('\\', '/');
                    var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(file);
                    fileStream.CopyTo(entryStream);
                }
            }

            return (true, "✅ EPUB archive created successfully.", ms.ToArray());
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to create EPUB: {ex.Message}", null);
        }
    }
}