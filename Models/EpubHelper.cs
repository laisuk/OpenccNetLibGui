using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace OpenccNetLibGui.Models;

public static class EpubHelper
{
    // ---------------------------- Format detection ----------------------------

    public static bool IsEpub(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        if (!path.EndsWith(".epub", StringComparison.OrdinalIgnoreCase)) return false;

        try
        {
            using var zip = ZipFile.OpenRead(path);
            return zip.GetEntry("META-INF/container.xml") != null;
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------- EPUB extraction ----------------------------

    public static string ExtractEpubAllText(
        string epubPath,
        bool includePartHeadings = false,
        bool normalizeNewlines = true,
        bool skipNavDocuments = true)
    {
        using var zip = ZipFile.OpenRead(epubPath);

        var opfPath = FindOpfPath(zip);
        if (opfPath == null)
            throw new InvalidOperationException("container.xml has no OPF rootfile. Not a valid .epub?");

        var opfDir = GetDir(opfPath);

        var (manifest, spine) = LoadOpf(zip, opfPath);

        var sb = new StringBuilder(256 * 1024);

        foreach (var idref in spine)
        {
            if (!manifest.TryGetValue(idref, out var item))
                continue;

            // Usually XHTML: application/xhtml+xml, but allow html, xhtml, xml
            if (!LooksLikeHtml(item.MediaType, item.Href))
                continue;

            if (skipNavDocuments && item.IsNav)
                continue;

            var fullName = CombineZipPath(opfDir, item.Href);
            var entry = zip.GetEntry(fullName);
            if (entry == null) continue;

            if (includePartHeadings)
            {
                if (sb.Length > 0 && !EndsWithNewline(sb)) sb.AppendLine();
                sb.AppendLine($"=== {fullName} ===");
            }

            using var s = entry.Open();
            var chapterText = ExtractXhtmlText(s);

            sb.Append(chapterText);
            if (!EndsWithNewline(sb)) sb.AppendLine();
            sb.AppendLine(); // blank line between spine docs
        }

        var text = sb.ToString();
        if (normalizeNewlines)
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        text = NormalizeExcessBlankLines(text);
        return text;
    }

    // ---------------------------- container.xml ----------------------------

    private static string? FindOpfPath(ZipArchive zip)
    {
        var entry = zip.GetEntry("META-INF/container.xml");
        if (entry == null) return null;

        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = false,
            DtdProcessing = DtdProcessing.Prohibit
        });

        // container.xml uses: urn:oasis:names:tc:opendocument:xmlns:container
        while (reader.Read())
        {
            if (reader is not { NodeType: XmlNodeType.Element, LocalName: "rootfile" }) continue;
            var fullPath = reader.GetAttribute("full-path") ?? reader.GetAttribute("fullpath");
            if (!string.IsNullOrWhiteSpace(fullPath))
                return fullPath.Trim();
        }

        return null;
    }

    // ---------------------------- OPF parsing ----------------------------

    private sealed class ManifestItem
    {
        public string Href = "";
        public string MediaType = "";
        public bool IsNav;
    }

    private static (Dictionary<string, ManifestItem> manifest, List<string> spine) LoadOpf(
        ZipArchive zip, string opfPath)
    {
        var entry = zip.GetEntry(opfPath);
        if (entry == null)
            throw new InvalidOperationException($"OPF not found: {opfPath}");

        var manifest = new Dictionary<string, ManifestItem>(StringComparer.Ordinal);
        var spine = new List<string>(256);

        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = false,
            DtdProcessing = DtdProcessing.Ignore, // ✅ allow DOCTYPE, skip it
            XmlResolver = null // ✅ no external fetch
        });

        // OPF is usually in "http://www.idpf.org/2007/opf" but don't hard-require.
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element) continue;

            switch (reader.LocalName)
            {
                // manifest item
                case "item":
                {
                    var id = reader.GetAttribute("id");
                    var href = reader.GetAttribute("href");
                    var mt = reader.GetAttribute("media-type") ?? "";

                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(href))
                    {
                        var props = reader.GetAttribute("properties") ?? "";
                        var isNav = props.Split(' ')
                            .Any(p => p.Equals("nav", StringComparison.OrdinalIgnoreCase));

                        manifest[id] = new ManifestItem
                        {
                            Href = href,
                            MediaType = mt,
                            IsNav = isNav
                        };
                    }

                    break;
                }
                // spine itemref
                case "itemref":
                {
                    var idref = reader.GetAttribute("idref");
                    if (!string.IsNullOrWhiteSpace(idref))
                        spine.Add(idref);
                    break;
                }
            }
        }

        return (manifest, spine);
    }

    private static bool LooksLikeHtml(string mediaType, string href)
    {
        if (string.IsNullOrEmpty(mediaType))
            return href.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) ||
                   href.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                   href.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);
        if (mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase)) return true;
        if (mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase)) return true;
        if (mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)) return true; // extra tolerant

        return href.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) ||
               href.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
               href.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------- XHTML -> plain text ----------------------------

    private static string ExtractXhtmlText(Stream xhtmlStream)
    {
        var sb = new StringBuilder(32 * 1024);

        // Stackless simple state is enough for plain text.
        var skipDepth = 0; // inside script/style/head/svg/math etc.

        var settings = new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = false,
            DtdProcessing = DtdProcessing.Ignore, // ✅ allow DOCTYPE but ignore it
            XmlResolver = null // ✅ extra safety (no external fetch)
        };

        using var reader = XmlReader.Create(xhtmlStream, settings);

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                {
                    var name = reader.LocalName;

                    if (IsSkipElement(name))
                    {
                        skipDepth++;
                        if (!reader.IsEmptyElement)
                            continue;
                        // empty element, immediately pop
                        skipDepth--;
                        continue;
                    }

                    if (skipDepth > 0)
                    {
                        if (reader.IsEmptyElement)
                        {
                            /* no-op */
                        }

                        continue;
                    }

                    if (IsBlockElement(name))
                        EnsureParagraphBreak(sb);

                    if (name.Equals("br", StringComparison.OrdinalIgnoreCase))
                        sb.Append('\n');
                    break;
                }
                case XmlNodeType.Text or XmlNodeType.SignificantWhitespace when skipDepth > 0:
                    continue;
                case XmlNodeType.Text or XmlNodeType.SignificantWhitespace:
                {
                    var t = reader.Value;
                    if (!string.IsNullOrEmpty(t))
                        AppendNormalizedText(sb, t);
                    break;
                }
                case XmlNodeType.EndElement:
                {
                    var name = reader.LocalName;

                    if (skipDepth > 0)
                    {
                        if (IsSkipElement(name))
                            skipDepth--;
                        continue;
                    }

                    if (IsBlockElement(name))
                        EnsureParagraphBreak(sb);
                    break;
                }
            }
        }

        var text = sb.ToString();
        text = text.Replace("\u00AD", ""); // soft hyphen
        text = text.Replace("\u00A0", " "); // nbsp
        return text;
    }

    private static bool IsSkipElement(string localName)
    {
        // Ignore non-content / layout constructs.
        return localName.Equals("script", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("style", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("head", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("svg", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("math", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("noscript", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlockElement(string localName)
    {
        // Minimal set; stable across most EPUBs.
        return localName.Equals("p", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("div", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("section", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("article", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("blockquote", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("li", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("h1", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("h2", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("h3", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("h4", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("h5", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("h6", StringComparison.OrdinalIgnoreCase) ||
               localName.Equals("hr", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendNormalizedText(StringBuilder sb, string t)
    {
        // Preserve internal spaces but avoid runaway whitespace.
        // Note: we do NOT “CJK smart spacing” here; reflow handles later.
        for (var i = 0; i < t.Length; i++)
        {
            var c = t[i];
            if (char.IsWhiteSpace(c))
            {
                if (sb.Length == 0) continue;
                var last = sb[^1];
                if (last is ' ' or '\n' or '\r' or '\t')
                    continue;
                sb.Append(' ');
            }
            else
            {
                sb.Append(c);
            }
        }
    }

    private static void EnsureParagraphBreak(StringBuilder sb)
    {
        // Ensure at least one newline; for paragraphs we want blank-line separation.
        TrimTrailingSpaces(sb);

        if (sb.Length == 0) return;

        // If already has a blank line at end, keep.
        var n = sb.Length;
        if (n >= 2 && sb[n - 1] == '\n' && sb[n - 2] == '\n')
            return;

        if (sb[^1] != '\n')
            sb.Append('\n');

        // Make it blank-line separated.
        sb.Append('\n');
    }

    private static void TrimTrailingSpaces(StringBuilder sb)
    {
        while (sb.Length > 0)
        {
            var c = sb[^1];
            if (c is ' ' or '\t')
                sb.Length--;
            else
                break;
        }
    }

    private static bool EndsWithNewline(StringBuilder sb)
    {
        if (sb.Length == 0) return true;
        var c = sb[^1];
        return c is '\n' or '\r';
    }

    private static string GetDir(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? "" : path[..(idx + 1)];
    }

    private static string CombineZipPath(string? dir, string? href)
    {
        // href may contain ../
        var raw = (dir ?? "") + (href ?? "");
        raw = raw.Replace('\\', '/');

        // Normalize ./ and ../
        var parts = new List<string>(raw.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
        var stack = new List<string>(parts.Count);

        foreach (var p in parts)
        {
            switch (p)
            {
                case ".":
                    continue;
                case "..":
                {
                    if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                default:
                    stack.Add(p);
                    break;
            }
        }

        return string.Join("/", stack);
    }

    private static string NormalizeExcessBlankLines(string s)
    {
        // Keep at most 2 consecutive newlines.
        var sb = new StringBuilder(s.Length);
        var nl = 0;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '\n')
            {
                nl++;
                if (nl <= 2) sb.Append(c);
            }
            else
            {
                nl = 0;
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}