using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace OpenccNetLibGui.Models;

public static class OpenXmlHelper
{
    // ---------------------------- Format detection ----------------------------

    public static bool IsDocx(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        if (!path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)) return false;

        try
        {
            using var zip = ZipFile.OpenRead(path);
            return zip.GetEntry("word/document.xml") != null
                   && zip.GetEntry("[Content_Types].xml") != null;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsOdt(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        if (!path.EndsWith(".odt", StringComparison.OrdinalIgnoreCase)) return false;

        try
        {
            using var zip = ZipFile.OpenRead(path);

            var content = zip.GetEntry("content.xml");
            if (content == null) return false;

            // Optional mimetype verification ( the best effort)
            var mimetype = zip.GetEntry("mimetype");
            if (mimetype != null)
            {
                using var s = mimetype.Open();
                using var r = new StreamReader(s, Encoding.ASCII, detectEncodingFromByteOrderMarks: false);
                var mt = r.ReadToEnd().Trim();
                if (!mt.Equals("application/vnd.oasis.opendocument.text", StringComparison.Ordinal))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------- DOCX extraction ----------------------------

    public static string ExtractDocxAllText(
        string docxPath,
        bool includePartHeadings = false,
        bool normalizeNewlines = true)
    {
        using var zip = ZipFile.OpenRead(docxPath);

        var ctx = NumberingContext.Load(zip);

        var parts = new List<string>();
        Add(parts, "word/document.xml");
        Add(parts, "word/footnotes.xml");
        Add(parts, "word/endnotes.xml");
        Add(parts, "word/comments.xml");

        parts.AddRange(zip.Entries
            .Where(e => e.FullName.StartsWith("word/header", StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.FullName)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));

        parts.AddRange(zip.Entries
            .Where(e => e.FullName.StartsWith("word/footer", StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.FullName)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));

        parts = parts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var output = new StringBuilder(128 * 1024);

        foreach (var partName in parts)
        {
            var entry = zip.GetEntry(partName);
            if (entry == null) continue;

            if (includePartHeadings)
            {
                if (output.Length > 0 && !EndsWithNewline(output))
                    output.AppendLine();
                output.AppendLine($"=== {partName} ===");
            }

            using var stream = entry.Open();

            // Reset counters per part
            ctx.ResetCountersForPart();

            var text = ExtractWordprocessingMlText(stream, ctx);
            output.Append(text);

            if (!EndsWithNewline(output))
                output.AppendLine();
        }

        var result = output.ToString();
        if (normalizeNewlines)
            result = result.Replace("\r\n", "\n").Replace("\r", "\n");

        return result;
    }

    private static void Add(List<string> parts, string name) => parts.Add(name);

    private static string ExtractWordprocessingMlText(Stream xmlStream, NumberingContext ctx)
    {
        const string nsW = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        var sb = new StringBuilder(64 * 1024);

        bool inTable = false, inRow = false, inCell = false;
        List<string>? currentRowCells = null;
        StringBuilder? currentCell = null;

        var inParagraph = false;
        var paraPrefixEmitted = false;

        int? paraNumId = null;
        int? paraIlvl = null;
        string? paraStyleId = null;

        var inFootnote = false;
        var inEndnote = false;
        var skipThisNote = false;

        StringBuilder CurrentTarget() => (inCell && currentCell != null) ? currentCell : sb;

        var settings = new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = false,
            DtdProcessing = DtdProcessing.Prohibit
        };

        using var reader = XmlReader.Create(xmlStream, settings);

        while (reader.Read())
        {
            if (!string.Equals(reader.NamespaceURI, nsW, StringComparison.Ordinal))
                continue;

            if (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.LocalName)
                {
                    case "footnote":
                        inFootnote = true;
                        skipThisNote = ShouldSkipNoteElement(reader);
                        break;

                    case "endnote":
                        inEndnote = true;
                        skipThisNote = ShouldSkipNoteElement(reader);
                        break;

                    case "tbl":
                        inTable = true;
                        break;

                    case "tr":
                        if (inTable)
                        {
                            inRow = true;
                            currentRowCells = new List<string>(8);
                        }

                        break;

                    case "tc":
                        if (inRow)
                        {
                            inCell = true;
                            currentCell = new StringBuilder(256);
                        }

                        break;

                    case "p":
                        inParagraph = true;
                        paraPrefixEmitted = false;
                        paraNumId = null;
                        paraIlvl = null;
                        paraStyleId = null;
                        break;

                    case "pStyle":
                        if (inParagraph)
                        {
                            var val = reader.GetAttribute("val", nsW) ?? reader.GetAttribute("w:val");
                            if (!string.IsNullOrEmpty(val))
                                paraStyleId = val;
                        }

                        break;

                    case "numId":
                        if (inParagraph)
                        {
                            var val = reader.GetAttribute("val", nsW) ?? reader.GetAttribute("w:val");
                            if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                                paraNumId = id;
                        }

                        break;

                    case "ilvl":
                        if (inParagraph)
                        {
                            var val = reader.GetAttribute("val", nsW) ?? reader.GetAttribute("w:val");
                            if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lvl))
                                paraIlvl = lvl;
                        }

                        break;

                    case "t":
                        if (skipThisNote && (inFootnote || inEndnote))
                        {
                            reader.Skip();
                            break;
                        }

                        EmitPrefixIfNeeded();
                        CurrentTarget().Append(reader.ReadElementContentAsString());
                        break;

                    case "tab":
                        if (skipThisNote && (inFootnote || inEndnote)) break;
                        EmitPrefixIfNeeded();
                        CurrentTarget().Append('\t');
                        break;

                    case "br":
                    case "cr":
                        if (skipThisNote && (inFootnote || inEndnote)) break;
                        EmitPrefixIfNeeded();
                        CurrentTarget().Append('\n');
                        break;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                switch (reader.LocalName)
                {
                    case "p":
                        if (!(skipThisNote && (inFootnote || inEndnote)))
                        {
                            CurrentTarget().Append('\n');
                        }

                        inParagraph = false;
                        break;

                    case "tc":
                        if (inCell && currentRowCells != null && currentCell != null)
                        {
                            currentRowCells.Add(TrimTrailingNewlines(currentCell.ToString()));
                            currentCell = null;
                            inCell = false;
                        }

                        break;

                    case "tr":
                        if (inRow && currentRowCells != null)
                        {
                            sb.Append(string.Join("\t", currentRowCells));
                            sb.Append('\n');
                            currentRowCells = null;
                            inRow = false;
                        }

                        break;

                    case "tbl":
                        if (inTable)
                        {
                            if (!EndsWithNewline(sb)) sb.Append('\n');
                            inTable = false;
                        }

                        break;

                    case "footnote":
                        inFootnote = false;
                        skipThisNote = false;
                        break;

                    case "endnote":
                        inEndnote = false;
                        skipThisNote = false;
                        break;
                }
            }
        }

        return sb.ToString();

        void EmitPrefixIfNeeded()
        {
            if (!inParagraph || paraPrefixEmitted) return;

            var (numId, ilvl) = ctx.ResolveNum(paraNumId, paraIlvl, paraStyleId);
            if (numId.HasValue && ilvl.HasValue)
            {
                var prefix = ctx.NextPrefix(numId.Value, ilvl.Value);
                if (!string.IsNullOrEmpty(prefix))
                {
                    CurrentTarget().Append(prefix);
                    paraPrefixEmitted = true;
                }
            }
        }
    }

    private static bool ShouldSkipNoteElement(XmlReader r)
    {
        const string nsW = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        var type = r.GetAttribute("type", nsW) ?? r.GetAttribute("w:type");
        if (!string.IsNullOrEmpty(type) &&
            (type.Equals("separator", StringComparison.OrdinalIgnoreCase) ||
             type.Equals("continuationSeparator", StringComparison.OrdinalIgnoreCase)))
            return true;

        var idStr = r.GetAttribute("id", nsW) ?? r.GetAttribute("w:id");
        if (int.TryParse(idStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            return id <= 0;

        return false;
    }

    // ---------------------------- ODT extraction ----------------------------

    public static string ExtractOdtAllText(string odtPath, bool normalizeNewlines = true)
    {
        using var zip = ZipFile.OpenRead(odtPath);
        var entry = zip.GetEntry("content.xml");
        if (entry == null)
            throw new InvalidOperationException("content.xml not found. Not a valid .odt?");

        using var stream = entry.Open();
        var text = ExtractOdfContentXml(stream);

        if (normalizeNewlines)
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        return text;
    }

    private static string ExtractOdfContentXml(Stream contentXml)
    {
        const string nsText = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        const string nsTable = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";

        var sb = new StringBuilder(64 * 1024);

        int listLevel = 0;

        bool inTable = false, inRow = false, inCell = false;
        List<string>? rowCells = null;
        StringBuilder? cellBuf = null;

        bool inParagraph = false;
        bool prefixEmitted = false;

        StringBuilder Target() => (inCell && cellBuf != null) ? cellBuf : sb;


        var settings = new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = false,
            DtdProcessing = DtdProcessing.Prohibit
        };

        using var reader = XmlReader.Create(contentXml, settings);

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.NamespaceURI == nsText)
                {
                    switch (reader.LocalName)
                    {
                        case "list":
                            listLevel++;
                            break;

                        case "p":
                        case "h":
                            inParagraph = true;
                            prefixEmitted = false;
                            EmitListPrefixIfNeeded();
                            break;

                        case "tab":
                            EmitListPrefixIfNeeded();
                            Target().Append('\t');
                            break;

                        case "line-break":
                            EmitListPrefixIfNeeded();
                            Target().Append('\n');
                            break;

                        case "s":
                            EmitListPrefixIfNeeded();
                            var cAttr = reader.GetAttribute("c", nsText) ?? reader.GetAttribute("text:c");
                            int count = 1;
                            if (int.TryParse(cAttr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) &&
                                n > 0)
                                count = n;
                            Target().Append(' ', count);
                            break;
                    }
                }

                if (reader.NamespaceURI == nsTable)
                {
                    switch (reader.LocalName)
                    {
                        case "table":
                            inTable = true;
                            break;

                        case "table-row":
                            if (inTable)
                            {
                                inRow = true;
                                rowCells = new List<string>(8);
                            }

                            break;

                        case "table-cell":
                            if (inRow)
                            {
                                inCell = true;
                                cellBuf = new StringBuilder(128);
                            }

                            break;
                    }
                }
            }
            else if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.SignificantWhitespace)
            {
                EmitListPrefixIfNeeded();
                Target().Append(reader.Value);
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (reader.NamespaceURI == nsText)
                {
                    switch (reader.LocalName)
                    {
                        case "list":
                            if (listLevel > 0) listLevel--;
                            break;

                        case "p":
                        case "h":
                            Target().Append('\n');
                            inParagraph = false;
                            break;
                    }
                }

                if (reader.NamespaceURI == nsTable)
                {
                    switch (reader.LocalName)
                    {
                        case "table-cell":
                            if (inCell && rowCells != null && cellBuf != null)
                            {
                                rowCells.Add(TrimTrailingNewlines(cellBuf.ToString()));
                                cellBuf = null;
                                inCell = false;
                            }

                            break;

                        case "table-row":
                            if (inRow && rowCells != null)
                            {
                                sb.Append(string.Join("\t", rowCells));
                                sb.Append('\n');
                                rowCells = null;
                                inRow = false;
                            }

                            break;

                        case "table":
                            if (inTable)
                            {
                                if (!EndsWithNewline(sb)) sb.Append('\n');
                                inTable = false;
                            }

                            break;
                    }
                }
            }
        }

        return sb.ToString();

        void EmitListPrefixIfNeeded()
        {
            if (!inParagraph || prefixEmitted) return;

            if (listLevel > 0)
            {
                Target().Append(new string(' ', (listLevel - 1) * 2));
                Target().Append("- ");
            }

            prefixEmitted = true;
        }
    }

    // -------------------------- Shared small helpers --------------------------

    private static bool EndsWithNewline(StringBuilder sb)
    {
        if (sb.Length == 0) return true;
        var c = sb[^1];
        return c is '\n' or '\r';
    }

    private static string TrimTrailingNewlines(string s)
    {
        int i = s.Length;
        while (i > 0)
        {
            char c = s[i - 1];
            if (c == '\n' || c == '\r') i--;
            else break;
        }

        return i == s.Length ? s : s.Substring(0, i);
    }

    // -------------------------- Numbering support --------------------------

    private sealed class NumberingContext
    {
        private readonly Dictionary<int, int> _numToAbstract = new();
        private readonly Dictionary<int, Dictionary<int, LevelDef>> _abstractLevels = new();
        private readonly Dictionary<string, (int numId, int ilvl)> _styleNum = new(StringComparer.Ordinal);
        private readonly Dictionary<int, int[]> _counters = new();

        public void ResetCountersForPart() => _counters.Clear();

        public (int? numId, int? ilvl) ResolveNum(int? directNumId, int? directIlvl, string? styleId)
        {
            if (directNumId.HasValue && directIlvl.HasValue)
                return (directNumId, directIlvl);

            if (!string.IsNullOrEmpty(styleId) && _styleNum.TryGetValue(styleId, out var s))
                return (s.numId, s.ilvl);

            return (null, null);
        }

        public string NextPrefix(int numId, int ilvl)
        {
            if (ilvl < 0) ilvl = 0;
            if (ilvl > 8) ilvl = 8;

            if (!_numToAbstract.TryGetValue(numId, out var absId)) return "";
            if (!_abstractLevels.TryGetValue(absId, out var lvls) || !lvls.TryGetValue(ilvl, out var def)) return "";

            var counters = _counters.TryGetValue(numId, out var arr) ? arr : (_counters[numId] = new int[9]);

            counters[ilvl]++;
            for (int d = ilvl + 1; d < counters.Length; d++) counters[d] = 0;

            if (def.NumFmt.Equals("bullet", StringComparison.OrdinalIgnoreCase))
                return "• ";

            var lvlText = string.IsNullOrEmpty(def.LvlText) ? "%1." : def.LvlText;

            string prefix = Regex.Replace(lvlText, @"%([1-9])", m =>
            {
                int k = (m.Groups[1].Value[0] - '1');
                int v = counters[k];
                if (v <= 0) v = 1;
                return v.ToString(CultureInfo.InvariantCulture);
            });

            prefix = prefix.Replace("\t", " ").Replace("\u00A0", " ");
            if (prefix.Length > 0 && !char.IsWhiteSpace(prefix[^1])) prefix += " ";
            return prefix;
        }

        public static NumberingContext Load(ZipArchive zip)
        {
            var ctx = new NumberingContext();
            ctx.LoadNumbering(zip);
            ctx.LoadStyles(zip);
            return ctx;
        }

        private void LoadNumbering(ZipArchive zip)
        {
            var entry = zip.GetEntry("word/numbering.xml");
            if (entry == null) return;

            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = false,
                DtdProcessing = DtdProcessing.Prohibit
            });

            const string nsW = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

            int? currentAbstractId = null;
            int? currentLevel = null;

            while (reader.Read())
            {
                if (!string.Equals(reader.NamespaceURI, nsW, StringComparison.Ordinal))
                    continue;

                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.LocalName)
                    {
                        case "num":
                        {
                            var numIdStr = reader.GetAttribute("numId", nsW) ?? reader.GetAttribute("w:numId");
                            if (int.TryParse(numIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                    out var numId))
                            {
                                int thisNumId = numId;

                                using var sub = reader.ReadSubtree();
                                sub.Read();
                                while (sub.Read())
                                {
                                    if (sub is
                                        {
                                            NodeType: XmlNodeType.Element, NamespaceURI: nsW, LocalName: "abstractNumId"
                                        })
                                    {
                                        var val = sub.GetAttribute("val", nsW) ?? sub.GetAttribute("w:val");
                                        if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                                out var absId))
                                            _numToAbstract[thisNumId] = absId;
                                    }
                                }
                            }

                            break;
                        }

                        case "abstractNum":
                        {
                            var absIdStr = reader.GetAttribute("abstractNumId", nsW) ??
                                           reader.GetAttribute("w:abstractNumId");
                            if (int.TryParse(absIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                    out var absId))
                            {
                                currentAbstractId = absId;
                                if (!_abstractLevels.ContainsKey(absId))
                                    _abstractLevels[absId] = new Dictionary<int, LevelDef>();
                            }

                            break;
                        }

                        case "lvl":
                            if (currentAbstractId.HasValue)
                            {
                                var ilvlStr = reader.GetAttribute("ilvl", nsW) ?? reader.GetAttribute("w:ilvl");
                                if (int.TryParse(ilvlStr, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                        out var ilvl))
                                {
                                    currentLevel = ilvl;
                                    if (!_abstractLevels[currentAbstractId.Value].ContainsKey(ilvl))
                                        _abstractLevels[currentAbstractId.Value][ilvl] = new LevelDef();
                                }
                            }

                            break;

                        case "numFmt":
                            if (currentAbstractId.HasValue && currentLevel.HasValue)
                            {
                                var val = reader.GetAttribute("val", nsW) ?? reader.GetAttribute("w:val") ?? "";
                                _abstractLevels[currentAbstractId.Value][currentLevel.Value].NumFmt = val;
                            }

                            break;

                        case "lvlText":
                            if (currentAbstractId.HasValue && currentLevel.HasValue)
                            {
                                var val = reader.GetAttribute("val", nsW) ?? reader.GetAttribute("w:val") ?? "";
                                _abstractLevels[currentAbstractId.Value][currentLevel.Value].LvlText = val;
                            }

                            break;
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    if (reader.LocalName == "abstractNum")
                    {
                        currentAbstractId = null;
                        currentLevel = null;
                    }
                    else if (reader.LocalName == "lvl")
                    {
                        currentLevel = null;
                    }
                }
            }
        }

        private void LoadStyles(ZipArchive zip)
        {
            var entry = zip.GetEntry("word/styles.xml");
            if (entry == null) return;

            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = false,
                DtdProcessing = DtdProcessing.Prohibit
            });

            const string nsW = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

            string? currentStyleId = null;
            int? styleNumId = null;
            int? styleIlvl = null;

            while (reader.Read())
            {
                if (!string.Equals(reader.NamespaceURI, nsW, StringComparison.Ordinal))
                    continue;

                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.LocalName)
                    {
                        case "style":
                            currentStyleId = reader.GetAttribute("styleId", nsW) ?? reader.GetAttribute("w:styleId");
                            styleNumId = null;
                            styleIlvl = null;
                            break;

                        case "numId":
                            if (currentStyleId != null)
                            {
                                var v = reader.GetAttribute("val", nsW) ?? reader.GetAttribute("w:val");
                                if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                                    styleNumId = id;
                            }

                            break;

                        case "ilvl":
                            if (currentStyleId != null)
                            {
                                var v = reader.GetAttribute("val", nsW) ?? reader.GetAttribute("w:val");
                                if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lvl))
                                    styleIlvl = lvl;
                            }

                            break;
                    }
                }
                else if (reader is { NodeType: XmlNodeType.EndElement, LocalName: "style" })
                {
                    if (!string.IsNullOrEmpty(currentStyleId) && styleNumId.HasValue && styleIlvl.HasValue)
                        _styleNum[currentStyleId] = (styleNumId.Value, styleIlvl.Value);

                    currentStyleId = null;
                    styleNumId = null;
                    styleIlvl = null;
                }
            }
        }

        private sealed class LevelDef
        {
            public string NumFmt = "";
            public string LvlText = "";
        }
    }
}