using System.Text.RegularExpressions;

namespace MilovanaEosEditor.Dsl;

/// <summary>
/// Turns raw <c>script.md</c> text into a flat list of <see cref="MarkerInstance"/>. Mirrors the
/// tokenizer in <c>Build-Tease.ps1</c>: only lines starting at column 0 with a recognised
/// <c>[KEYWORD …]</c> are markers; a marker may span several lines (its closing <c>]</c> on its own
/// line); everything else (prose, notes) is ignored. Unlike the PS version it also records absolute
/// character spans so the editor can colorize and squiggle precisely.
/// </summary>
public sealed partial class TeaseScriptParser
{
    private readonly CommandRegistry _registry;

    public TeaseScriptParser(CommandRegistry registry) => _registry = registry;

    // (?s): a marker's payload may span lines, so '.' matches '\n'; the lazy (.*?) still stops at the
    // first ']'. After ':' we consume at most ONE space so extra leading indent on a SAY's first line
    // is preserved (handled later by space-preservation), matching continuation lines.
    [GeneratedRegex(@"^\[(\w+)(?:\s*\(([^)]*)\))?\s*(?:: ?(.*?))?\]", RegexOptions.Singleline)]
    private static partial Regex MarkerRegex();

    public IReadOnlyList<MarkerInstance> Parse(string text, DiagnosticSink diags)
    {
        var markers = new List<MarkerInstance>();
        IReadOnlyList<(int Start, int Length)> lines = SplitLines(text);

        int i = 0;
        while (i < lines.Count)
        {
            (int lineStart, int lineLen) = lines[i];
            int lineNo = i + 1;
            string lineText = text.Substring(lineStart, lineLen);

            // An unclosed marker ('[' at column 0 with no ']' yet) continues to the line that closes it.
            int recStart = lineStart, recEnd = lineStart + lineLen, advance = 1;
            if (lineText.StartsWith('[') && !lineText.Contains(']'))
            {
                int j = i + 1;
                while (j < lines.Count && !text.Substring(lines[j].Start, lines[j].Length).Contains(']')) j++;
                if (j < lines.Count)
                {
                    recEnd = lines[j].Start + lines[j].Length;
                    advance = j + 1 - i;
                }
                else
                {
                    diags.Error(lineNo, DiagnosticKind.MarkerUnclosed,
                        "marker '[' is never closed with ']' before end of file");
                    recEnd = lines[^1].Start + lines[^1].Length;
                    advance = lines.Count - i;
                }
            }

            i += advance;

            string recText = text.Substring(recStart, recEnd - recStart);
            Match m = MarkerRegex().Match(recText);
            if (!m.Success)
            {
                // A line that opens with '[' at column 0 but doesn't parse is almost always a typo'd
                // marker rather than prose (author notes are exempt).
                if (recText.StartsWith('[') && !recText.StartsWith("[Author note", StringComparison.OrdinalIgnoreCase))
                {
                    string first = recText.Split('\n')[0].Trim();
                    diags.Warn(lineNo, DiagnosticKind.MarkerNotParsed,
                        $"line looks like a marker but did not parse; ignored: {first}");
                }
                continue;
            }

            string keyword = m.Groups[1].Value.ToUpperInvariant();
            Group paramGroup = m.Groups[2];
            Group payloadGroup = m.Groups[3];

            string rawParams = paramGroup.Success ? paramGroup.Value : "";
            IReadOnlyList<ParamToken> paramTokens = paramGroup.Success
                ? ParseParams(rawParams, recStart + paramGroup.Index)
                : Array.Empty<ParamToken>();

            markers.Add(new MarkerInstance
            {
                Keyword = keyword,
                Command = _registry.Find(keyword),
                RawParams = rawParams,
                Params = paramTokens,
                Payload = payloadGroup.Success ? payloadGroup.Value : "",
                PayloadOffset = payloadGroup.Success ? recStart + payloadGroup.Index : -1,
                PayloadLength = payloadGroup.Success ? payloadGroup.Length : 0,
                Line = lineNo,
                StartOffset = recStart + m.Index,
                Length = m.Length,
            });
        }

        return markers;
    }

    /// <summary>Split into (start, length) per line, excluding the line terminator (\n, \r\n, or \r).</summary>
    private static List<(int Start, int Length)> SplitLines(string text)
    {
        var lines = new List<(int, int)>();
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n' || c == '\r')
            {
                lines.Add((start, i - start));
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                start = i + 1;
            }
        }
        lines.Add((start, text.Length - start));
        return lines;
    }

    /// <summary>Parse a <c>name=value, name=value</c> segment into tokens with document offsets. Segments
    /// without '=' are dropped here (the base command validation flags them from the raw text).</summary>
    private static List<ParamToken> ParseParams(string raw, int absStart)
    {
        var tokens = new List<ParamToken>();
        int pos = 0;
        while (pos <= raw.Length)
        {
            int comma = raw.IndexOf(',', pos);
            int segEnd = comma < 0 ? raw.Length : comma;
            string seg = raw.Substring(pos, segEnd - pos);
            int eq = seg.IndexOf('=');
            if (eq >= 0)
            {
                string namePart = seg[..eq];
                string valuePart = seg[(eq + 1)..];
                string name = namePart.Trim();
                string value = valuePart.Trim();
                if (name.Length > 0)
                {
                    int nameOffset = absStart + pos + IndexOfFirstNonSpace(namePart);
                    int valueOffset = absStart + pos + eq + 1 + IndexOfFirstNonSpace(valuePart);
                    tokens.Add(new ParamToken(name, value, nameOffset, valueOffset, value.Length));
                }
            }
            if (comma < 0) break;
            pos = comma + 1;
        }
        return tokens;
    }

    private static int IndexOfFirstNonSpace(string s)
    {
        int i = 0;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        return i == s.Length ? 0 : i;
    }
}
