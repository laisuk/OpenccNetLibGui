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

        return ExtractEpubAllText(zip, includePartHeadings, normalizeNewlines, skipNavDocuments);
    }

    private static string ExtractEpubAllText(
        ZipArchive zip, bool includePartHeadings, bool normalizeNewlines, bool skipNavDocuments)
    {
        var opfPath = FindOpfPath(zip);
        if (opfPath == null)
            throw new InvalidOperationException("container.xml has no OPF rootfile. Not a valid .epub?");

        var opfDir = GetDir(opfPath);

        var (manifest, spine) = LoadOpf(zip, opfPath);

        var output = new EpubTextAccumulator(normalizeNewlines);
        var chapter = new StringBuilder();
        var entityBuffer = new char[XhtmlEntityTextReader.BufferSize];
        var textBuffer = new char[4096];

        foreach (var idref in spine)
        {
            if (!manifest.TryGetValue(idref, out var item))
                continue;

            // Usually XHTML: application/xhtml+xml, but allow HTML, xhtml, XML
            if (!LooksLikeHtml(item.MediaType, item.Href))
                continue;

            if (skipNavDocuments && item.IsNav)
                continue;

            var fullName = CombineZipPath(opfDir, item.Href);
            var entry = zip.GetEntry(fullName);
            if (entry == null) continue;

            if (includePartHeadings)
            {
                if (!output.EndsWithNewline) output.Append(Environment.NewLine);
                output.Append($"=== {fullName} ===");
                output.Append(Environment.NewLine);
            }

            ExtractXhtmlText(entry.Open(), chapter, entityBuffer, textBuffer);
            foreach (var chunk in chapter.GetChunks())
                output.Append(chunk.Span, removeSoftHyphens: true);

            if (!output.EndsWithNewline) output.Append(Environment.NewLine);
            output.Append(Environment.NewLine); // blank line between spine docs

            // Keep a small chapter buffer for reuse without retaining very large chapters.
            if (chapter.Capacity <= 8192)
                chapter.Clear();
            else
                chapter = new StringBuilder();
        }

        return output.ToString();
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

    private static void ExtractXhtmlText(
        Stream xhtmlStream, StringBuilder sb, char[] entityBuffer, char[] textBuffer)
    {
        using var textReader = new XhtmlEntityTextReader(
            new StreamReader(xhtmlStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true),
            entityBuffer);

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

        using var reader = XmlReader.Create(textReader, settings);

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

                    if (skipDepth > 0) continue;

                    if (IsBlockElement(name))
                        EnsureParagraphBreak(sb);

                    if (name.Equals("br", StringComparison.OrdinalIgnoreCase))
                        sb.Append('\n');
                    break;
                }
                case XmlNodeType.Text or XmlNodeType.SignificantWhitespace
                    or XmlNodeType.Whitespace when skipDepth > 0:
                    continue;

                case XmlNodeType.Text or XmlNodeType.SignificantWhitespace
                    or XmlNodeType.Whitespace:
                {
                    int count;
                    while ((count = reader.ReadValueChunk(textBuffer, 0, textBuffer.Length)) > 0)
                        AppendNormalizedText(sb, textBuffer.AsSpan(0, count));
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

    private static void AppendNormalizedText(StringBuilder sb, ReadOnlySpan<char> t)
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

    private sealed class EpubTextAccumulator
    {
        private readonly StringBuilder _text = new();
        private readonly bool _normalizeNewlines;
        private bool _previousWasCarriageReturn;
        private int _newlines;

        public EpubTextAccumulator(bool normalizeNewlines) => _normalizeNewlines = normalizeNewlines;

        public bool EndsWithNewline => _text.Length == 0 || _text[^1] is '\n' or '\r';

        public void Append(ReadOnlySpan<char> text, bool removeSoftHyphens = false)
        {
            // Soft hyphens influence chapter-local whitespace decisions before removal.
            // Non-breaking spaces have already been collapsed by AppendNormalizedText.
            while (!text.IsEmpty)
            {
                var special = removeSoftHyphens
                    ? text.IndexOfAny('\r', '\n', '\u00AD')
                    : text.IndexOfAny('\r', '\n');
                if (special < 0) special = text.Length;
                if (special > 0)
                {
                    _text.Append(text[..special]);
                    _previousWasCarriageReturn = false;
                    _newlines = 0;
                }

                if (special == text.Length) return;
                var current = text[special];
                text = text[(special + 1)..];
                if (current == '\u00AD') continue;

                var followsCarriageReturn = _previousWasCarriageReturn;
                _previousWasCarriageReturn = current == '\r';
                if (_normalizeNewlines)
                {
                    if (current == '\n' && followsCarriageReturn) continue;
                    current = '\n';
                }

                if (current == '\r')
                {
                    _text.Append(current);
                    _newlines = 0;
                }
                else if (_newlines < 2)
                {
                    _text.Append(current);
                    _newlines++;
                }
            }
        }

        public override string ToString() => _text.ToString();
    }
}
