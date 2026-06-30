using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using MilovanaEosEditor.Dsl;

namespace MilovanaEosEditor.Editor;

/// <summary>
/// Per-visual-line syntax highlighting for the marker DSL, matching the design mockup's palette
/// (keyword blue, [PAGE] purple-bold, param names teal, values/buckets green, numbers, nav targets
/// brown, punctuation grey, say text grey). Colours each line independently from the marker grammar so
/// it stays correct while the document is edited; multi-line marker continuation lines render plain.
/// </summary>
internal sealed partial class DslColorizer : DocumentColorizingTransformer
{
    private readonly CommandRegistry _registry;

    public DslColorizer(CommandRegistry registry) => _registry = registry;

    [GeneratedRegex(@"^\[(\w+)(?:\s*\(([^)]*)\))?\s*(?:: ?(.*?))?\]", RegexOptions.Singleline)]
    private static partial Regex MarkerRegex();

    [GeneratedRegex(@"^\[(\w+)")]
    private static partial Regex KeywordOnlyRegex();

    protected override void ColorizeLine(DocumentLine line)
    {
        string text = CurrentContext.Document.GetText(line);
        if (text.Length == 0 || text[0] != '[') return;
        int baseOffset = line.Offset;

        Match m = MarkerRegex().Match(text);
        if (!m.Success)
        {
            // Continuation/typo line: at least colour a known leading keyword.
            Match k = KeywordOnlyRegex().Match(text);
            if (k.Success && _registry.IsKnown(k.Groups[1].Value.ToUpperInvariant()))
            {
                Color(baseOffset, 1, EditorColors.Punctuation);
                ColorKeyword(baseOffset, k.Groups[1]);
            }
            return;
        }

        Group kw = m.Groups[1];
        Group prms = m.Groups[2];
        Group payload = m.Groups[3];
        MarkerCommand? cmd = _registry.Find(kw.Value.ToUpperInvariant());

        Color(baseOffset, 1, EditorColors.Punctuation);                          // '['
        Color(baseOffset + m.Length - 1, 1, EditorColors.Punctuation);           // ']'
        ColorKeyword(baseOffset, kw);

        if (prms.Success)
        {
            Color(baseOffset + prms.Index - 1, 1, EditorColors.Punctuation);      // '('
            Color(baseOffset + prms.Index + prms.Length, 1, EditorColors.Punctuation); // ')'
            ColorParams(prms.Value, baseOffset + prms.Index);
        }

        if (payload.Success)
        {
            int colon = text.LastIndexOf(':', payload.Index - 1);
            if (colon >= 0) Color(baseOffset + colon, 1, EditorColors.Punctuation);
            ColorPayload(cmd, payload.Value, baseOffset + payload.Index);
        }
    }

    private void ColorKeyword(int baseOffset, Group kw)
    {
        bool isPage = string.Equals(kw.Value, "PAGE", StringComparison.OrdinalIgnoreCase);
        Color(baseOffset + kw.Index, kw.Length, isPage ? EditorColors.Page : EditorColors.Keyword, bold: isPage);
    }

    private void ColorParams(string raw, int absStart)
    {
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
                int nameStart = NonSpace(namePart);
                Color(absStart + pos + nameStart, namePart.Trim().Length, EditorColors.ParamName);
                Color(absStart + pos + eq, 1, EditorColors.Punctuation); // '='
                string value = valuePart.Trim();
                if (value.Length > 0)
                    Color(absStart + pos + eq + 1 + NonSpace(valuePart), value.Length, ValueBrush(value));
            }
            if (comma >= 0) Color(absStart + comma, 1, EditorColors.Punctuation);
            if (comma < 0) break;
            pos = comma + 1;
        }
    }

    private static Brush ValueBrush(string value)
    {
        if (value.StartsWith('#')) return EditorColors.ColorLiteral;
        if (double.TryParse(value, out _)) return EditorColors.Number;
        return EditorColors.ParamValue;
    }

    private void ColorPayload(MarkerCommand? cmd, string payload, int absStart)
    {
        Brush brush = cmd?.Payload.Kind switch
        {
            PayloadKind.PageKey => EditorColors.Page,
            PayloadKind.NavTarget => EditorColors.NavTarget,
            PayloadKind.ImageRef => EditorColors.ParamValue,
            PayloadKind.Html or PayloadKind.Label => EditorColors.SayText,
            _ => EditorColors.SayText,
        };
        // Trim leading whitespace so indentation isn't coloured oddly.
        int lead = NonSpace(payload);
        if (payload.Trim().Length > 0)
            Color(absStart + lead, payload.TrimEnd().Length - lead, brush);
    }

    private void Color(int start, int length, Brush brush, bool bold = false)
    {
        if (length <= 0) return;
        ChangeLinePart(start, start + length, el =>
        {
            el.TextRunProperties.SetForegroundBrush(brush);
            if (bold)
            {
                Typeface tf = el.TextRunProperties.Typeface;
                el.TextRunProperties.SetTypeface(new Typeface(tf.FontFamily, tf.Style, FontWeights.Bold, tf.Stretch));
            }
        });
    }

    private static int NonSpace(string s)
    {
        int i = 0;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        return i == s.Length ? 0 : i;
    }
}
