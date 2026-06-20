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
    /// Matches an XLSX inline-string cell:
    /// <![CDATA[<c ... t="inlineStr" ...>...</c>]]>
    /// </summary>
    private static readonly Regex XlsxInlineStringCellRegex = new(
        "<c\\b(?=[^>]*\\bt=(?:\"inlineStr\"|'inlineStr'))[^>]*>.*?</c>",
        RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    /// <summary>
    /// Matches a text node inside XLSX inline-string content:
    /// <![CDATA[<t ...>TEXT</t>]]>
    /// </summary>
    private static readonly Regex XlsxTextNodeRegex = new(
        "(<t\\b[^>]*>)(.*?)(</t>)",
        RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

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
        return !string.IsNullOrWhiteSpace(format) && OfficeFormats.Contains(format.Trim());
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
        ValidateInputBytes(inputBytes);
        ArgumentNullException.ThrowIfNull(converter);
        var normalizedFormat = NormalizeFormat(format);

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

                    var destPath = GetSafeExtractionPath(tempDir, entry);

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

                    await using var entryStream = entry.Open();
                    await using var fileStream = File.Create(destPath);
                    await entryStream.CopyToAsync(fileStream).ConfigureAwait(false);
                }
            }

            // Identify target XML/XHTML files for each Office/EPUB format
            var targetXmlPaths = normalizedFormat switch
            {
                "docx" => new List<string> { Path.Combine("word", "document.xml") },

                "xlsx" => CollectXlsxTargetXmlPaths(tempDir),

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

            if (targetXmlPaths == null || targetXmlPaths.Count == 0)
            {
                return (false, $"❌ Unsupported or invalid format: {format}", null);
            }

            var convertedCount = 0;

            // Process each target XML file
            foreach (var relativePath in targetXmlPaths)
            {
                var fullPath = Path.Combine(tempDir, relativePath);
                if (!File.Exists(fullPath))
                    continue;

                var xmlContent = await File.ReadAllTextAsync(fullPath, Encoding.UTF8)
                    .ConfigureAwait(false);

                Dictionary<string, string> fontMap = new();

                // Pre-process: replace font names with unique markers if keepFont is enabled
                if (keepFont && ShouldMaskFonts(normalizedFormat, relativePath))
                {
                    var fontCounter = 0;
                    var pattern = normalizedFormat switch
                    {
                        "docx" => @"(w:eastAsia=""|w:ascii=""|w:hAnsi=""|w:cs="")(.*?)("")",
                        "xlsx" => @"(val="")(.*?)("")",
                        "pptx" => @"(typeface="")(.*?)("")",
                        "odt" or "ods" or "odp" =>
                            @"((?:style:font-name(?:-asian|-complex)?|svg:font-family|style:name)=[""'])([^""']+)([""'])",
                        "epub" => @"(font-family\s*:\s*)([^;""']+)([;""'])?",
                        _ => null
                    };

                    if (pattern is not null)
                    {
                        xmlContent = Regex.Replace(xmlContent, pattern, match =>
                        {
                            var originalFont = match.Groups[2].Value;
                            var marker = $"__F_O_N_T_{fontCounter++}__";
                            fontMap[marker] = originalFont;

                            var suffix = match.Groups.Count >= 4 ? match.Groups[3].Value : string.Empty;
                            return match.Groups[1].Value + marker + suffix;
                        });
                    }
                }

                string convertedXml;

                if (normalizedFormat == "xlsx")
                {
                    convertedXml = ConvertXlsxXmlPart(xmlContent, relativePath, converter, punctuation);
                }
                else
                {
                    convertedXml = converter.Convert(xmlContent, punctuation);
                }

                // Post-process: restore font names from markers
                if (fontMap.Count > 0)
                {
                    foreach (var kvp in fontMap)
                    {
                        convertedXml = convertedXml.Replace(kvp.Key, kvp.Value, StringComparison.Ordinal);
                    }
                }

                // Overwrite the file with the converted content
                await File.WriteAllTextAsync(fullPath, convertedXml, Encoding.UTF8)
                    .ConfigureAwait(false);

                convertedCount++;
            }

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

            ValidateZipBytes(resultBytes);

            return (true,
                $"✅ Successfully converted {convertedCount} fragment(s) in {normalizedFormat} document.",
                resultBytes);
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
        ValidatePath(inputPath, nameof(inputPath));
        ValidatePath(outputPath, nameof(outputPath));
        var normalizedFormat = NormalizeFormat(format);
        ArgumentNullException.ThrowIfNull(converter);

        if (!File.Exists(inputPath))
        {
            return (false, $"❌ Input file not found: {inputPath}");
        }

        var inputBytes = await File.ReadAllBytesAsync(inputPath).ConfigureAwait(false);

        var (success, message, outputBytes) = await ConvertOfficeBytesAsync(
                inputBytes, normalizedFormat, converter, punctuation, keepFont)
            .ConfigureAwait(false);

        if (!success || outputBytes is null)
        {
            return (success, message);
        }

        await WriteAllBytesAtomicAsync(outputPath, outputBytes).ConfigureAwait(false);
        return (true, message);
    }

    /// <summary>Validates that an in-memory package was supplied.</summary>
    private static void ValidateInputBytes(byte[] inputBytes)
    {
        ArgumentNullException.ThrowIfNull(inputBytes);

        if (inputBytes.Length == 0)
            throw new ArgumentException("Input package bytes must not be empty.", nameof(inputBytes));
    }

    /// <summary>Validates a public file path argument.</summary>
    private static void ValidatePath(string path, string paramName)
    {
        ArgumentNullException.ThrowIfNull(path, paramName);

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty or whitespace.", paramName);
    }

    /// <summary>Validates and canonicalizes a logical Office or EPUB format name.</summary>
    private static string NormalizeFormat(string format)
    {
        ArgumentNullException.ThrowIfNull(format);

        var normalized = format.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
            throw new ArgumentException("Format must not be empty or whitespace.", nameof(format));
        if (!OfficeFormats.Contains(normalized))
            throw new ArgumentException($"Unsupported Office/EPUB format: '{normalized}'.", nameof(format));

        return normalized;
    }

    /// <summary>Returns a normalized extraction path that cannot escape the ZIP root.</summary>
    private static string GetSafeExtractionPath(string tempDir, ZipArchiveEntry entry)
    {
        var rootPath = Path.GetFullPath(tempDir);
        var rootWithSeparator = Path.EndsInDirectorySeparator(rootPath)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        var normalizedEntryName = entry.FullName
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var destinationPath = Path.GetFullPath(Path.Combine(rootPath, normalizedEntryName));
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!destinationPath.StartsWith(rootWithSeparator, pathComparison))
        {
            throw new InvalidDataException(
                $"ZIP entry escapes the extraction directory: '{entry.FullName}'.");
        }

        return destinationPath;
    }

    /// <summary>Confirms that generated bytes contain a readable ZIP package.</summary>
    private static void ValidateZipBytes(byte[] bytes)
    {
        ValidateInputBytes(bytes);

        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        _ = archive.Entries.Count;
    }

    /// <summary>Writes a complete package to a sibling file before atomically publishing it.</summary>
    private static async Task WriteAllBytesAtomicAsync(string outputPath, byte[] bytes)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath);
        if (string.IsNullOrEmpty(outputDirectory))
            throw new ArgumentException("Output path must include a valid directory.", nameof(outputPath));

        Directory.CreateDirectory(outputDirectory);
        var tempPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(fullOutputPath))
                File.Replace(tempPath, fullOutputPath, destinationBackupFileName: null);
            else
                File.Move(tempPath, fullOutputPath);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Collects XLSX XML parts that may contain user-visible text.
    /// Includes the shared string table and worksheet XML files for inline strings.
    /// </summary>
    private static List<string> CollectXlsxTargetXmlPaths(string tempDir)
    {
        var results = new List<string>();

        var sharedStringsPath = Path.Combine(tempDir, "xl", "sharedStrings.xml");
        if (File.Exists(sharedStringsPath))
            results.Add(Path.Combine("xl", "sharedStrings.xml"));

        var worksheetsDir = Path.Combine(tempDir, "xl", "worksheets");
        if (Directory.Exists(worksheetsDir))
        {
            results.AddRange(
                Directory.GetFiles(worksheetsDir, "*.xml", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(tempDir, path))
            );
        }

        return results;
    }

    /// <summary>
    /// Returns whether font masking should be applied for the given part.
    /// For XLSX, masking is limited to sharedStrings.xml only.
    /// </summary>
    private static bool ShouldMaskFonts(string normalizedFormat, string relativePath)
    {
        if (!string.Equals(normalizedFormat, "xlsx", StringComparison.Ordinal))
            return true;

        var normalizedPath = relativePath.Replace('\\', '/');
        return string.Equals(normalizedPath, "xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts an XLSX XML part using narrow rules:
    /// sharedStrings.xml is converted as a whole file,
    /// worksheet XML converts only inline-string cell text nodes,
    /// and all other XLSX XML parts are left unchanged.
    /// </summary>
    private static string ConvertXlsxXmlPart(
        string xmlContent,
        string relativePath,
        Opencc converter,
        bool punctuation)
    {
        var normalizedPath = relativePath.Replace('\\', '/');

        if (string.Equals(normalizedPath, "xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase))
        {
            return converter.Convert(xmlContent, punctuation);
        }

        if (normalizedPath.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) &&
            normalizedPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return XlsxInlineStringCellRegex.Replace(xmlContent, cellMatch =>
            {
                var cellXml = cellMatch.Value;

                return XlsxTextNodeRegex.Replace(cellXml, textMatch =>
                {
                    var openTag = textMatch.Groups[1].Value;
                    var innerText = textMatch.Groups[2].Value;
                    var closeTag = textMatch.Groups[3].Value;

                    if (string.IsNullOrEmpty(innerText))
                        return textMatch.Value;

                    var convertedText = converter.Convert(innerText, punctuation);
                    return openTag + convertedText + closeTag;
                });
            });
        }

        return xmlContent;
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