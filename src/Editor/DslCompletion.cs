using System.Text.RegularExpressions;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using MilovanaEosEditor.Dsl;

namespace MilovanaEosEditor.Editor;

/// <summary>
/// Ctrl+Space IntelliSense for the marker DSL. Works out the caret context from the current line and the
/// <see cref="CommandRegistry"/> + <see cref="AssetCatalog"/>, then offers: marker keywords at column 0,
/// parameter names/values inside <c>(...)</c>, buckets and filenames inside <c>[IMAGE:</c>, page names for
/// goto/option/notification targets, and metronome BPMs from the Files folder.
/// </summary>
internal static partial class DslCompletion
{
    // A handful of on-brand button colours offered for [OPTION color=].
    private static readonly string[] SuggestedColors =
        { "#1976d2", "#f06292", "#5d4037", "#2e7d32", "#d62246", "#e8a33d", "#f44336" };

    public static CompletionWindow? Show(TextEditor editor, AssetCatalog assets, IReadOnlyList<string> pageNames, CommandRegistry registry)
    {
        TextArea area = editor.TextArea;
        int caret = area.Caret.Offset;
        DocumentLine line = editor.Document.GetLineByOffset(caret);
        int caretCol = caret - line.Offset;
        string lineText = editor.Document.GetText(line.Offset, line.Length);

        Context? ctx = Analyze(lineText, caretCol, registry);
        if (ctx is null) return null;

        List<DslCompletionData> items = BuildItems(ctx, assets, pageNames, registry);
        if (items.Count == 0) return null;

        var window = new CompletionWindow(area)
        {
            StartOffset = line.Offset + ctx.TokenStartCol,
            EndOffset = caret,
        };
        foreach (DslCompletionData item in items) window.CompletionList.CompletionData.Add(item);
        // Preselect the closest match to what's already typed.
        window.CompletionList.SelectItem(ctx.Partial);
        window.Show();
        return window;
    }

    // ===== context analysis =====
    private enum Kind { Keyword, ParamName, ParamValue, ImageBucket, ImageFile, PageTarget, Bpm }

    private sealed record Context(Kind Kind, string Keyword, string ParamName, string Bucket, int TokenStartCol, string Partial);

    [GeneratedRegex(@"^\w*$")] private static partial Regex WordOnly();
    [GeneratedRegex(@"^(\w+)")] private static partial Regex LeadingWord();

    private static Context? Analyze(string line, int caretCol, CommandRegistry registry)
    {
        string prefix = line[..caretCol];
        int br = prefix.LastIndexOf('[');

        // No open bracket: offer keywords only when the line is empty/whitespace (markers are column 0).
        if (br < 0)
        {
            if (prefix.Trim().Length == 0) return new Context(Kind.Keyword, "", "", "", 0, prefix.TrimStart());
            return null;
        }
        if (br != 0) return null; // a real marker bracket is at column 0

        string after = prefix[1..]; // text after '['
        if (WordOnly().IsMatch(after))
            return new Context(Kind.Keyword, "", "", "", 0, after);

        string keyword = LeadingWord().Match(after).Groups[1].Value.ToUpperInvariant();
        int open = after.IndexOf('(');
        int close = after.LastIndexOf(')');
        int colon = after.IndexOf(':');

        // Inside an unclosed (...) parameter list.
        if (open >= 0 && close < open)
        {
            int segStart = Math.Max(after.LastIndexOf(','), open) + 1;
            string seg = after[segStart..];
            int eq = seg.IndexOf('=');
            if (eq < 0)
            {
                int lead = CountLeadingSpaces(seg);
                return new Context(Kind.ParamName, keyword, "", "", 1 + segStart + lead, seg.Trim());
            }
            string paramName = seg[..eq].Trim();
            string valPart = seg[(eq + 1)..];
            int valLead = CountLeadingSpaces(valPart);
            Kind valueKind = paramName is "target" ? Kind.PageTarget : paramName is "bpm" ? Kind.Bpm : Kind.ParamValue;
            return new Context(valueKind, keyword, paramName, "", 1 + segStart + eq + 1 + valLead, valPart.Trim());
        }

        // After the ':' — payload completion depends on the command's payload kind.
        if (colon >= 0 && (open < 0 || colon > close))
        {
            MarkerCommand? cmd = registry.Find(keyword);
            PayloadKind kind = cmd?.Payload.Kind ?? PayloadKind.None;
            int payStart = colon + 1;
            if (payStart < after.Length && after[payStart] == ' ') payStart++;
            string payload = after[payStart..];

            if (kind == PayloadKind.ImageRef)
            {
                int slash = payload.IndexOf('/');
                if (slash >= 0)
                {
                    string bucket = payload[..slash];
                    return new Context(Kind.ImageFile, keyword, "", bucket, 1 + payStart + slash + 1, payload[(slash + 1)..]);
                }
                return new Context(Kind.ImageBucket, keyword, "", "", 1 + payStart, payload);
            }
            if (kind == PayloadKind.NavTarget)
                return new Context(Kind.PageTarget, keyword, "", "", 1 + payStart, payload);
        }

        return null;
    }

    // ===== item construction =====
    private static List<DslCompletionData> BuildItems(Context ctx, AssetCatalog assets, IReadOnlyList<string> pageNames, CommandRegistry registry)
    {
        var items = new List<DslCompletionData>();
        switch (ctx.Kind)
        {
            case Kind.Keyword:
                foreach (MarkerCommand cmd in registry.All)
                {
                    string snippet = cmd.CompletionSnippet;
                    int caretFromEnd = snippet.EndsWith(": ") || snippet.Contains('(') ? 1 : 0;
                    items.Add(new DslCompletionData(cmd.Keyword, "[" + snippet + "]", cmd.Summary, "K", caretFromEnd));
                }
                break;

            case Kind.ParamName:
                foreach (ParameterSpec spec in registry.Find(ctx.Keyword)?.Parameters ?? Array.Empty<ParameterSpec>())
                    items.Add(new DslCompletionData(spec.Name, spec.Name + "=", spec.Description, "P", 0));
                break;

            case Kind.ParamValue:
                ParameterSpec? p = registry.Find(ctx.Keyword)?.Parameters.FirstOrDefault(x => x.Name == ctx.ParamName);
                if (ctx.ParamName == "color")
                    foreach (string c in SuggestedColors) items.Add(new DslCompletionData(c, c, "button colour", "#", 0));
                else if (p is { Kind: ParamValueKind.Enum })
                    foreach (string v in p.EnumValues) items.Add(new DslCompletionData(v, v, $"{ctx.ParamName} value", "V", 0));
                break;

            case Kind.PageTarget:
                foreach (string name in pageNames)
                    items.Add(new DslCompletionData(name, name, "page", "#", 0));
                break;

            case Kind.Bpm:
                foreach (int bpm in MetronomeBpms(assets))
                    items.Add(new DslCompletionData(bpm.ToString(), bpm.ToString(), $"metronome-{bpm:000}bpm.mp3", "♪", 0));
                break;

            case Kind.ImageBucket:
                foreach (string bucket in assets.Buckets)
                    items.Add(new DslCompletionData(bucket + "/", bucket + "/", $"{assets.ImageCountIn(bucket)} imgs · {assets.ThemeOf(bucket)}", "B", 0));
                break;

            case Kind.ImageFile:
                foreach (string file in assets.FilesIn(ctx.Bucket))
                {
                    ImageTags? tags = assets.TagsFor($"{ctx.Bucket}/{file}");
                    string desc = tags is null ? "" : $"{tags.Subject} · pace {tags.Pace} · {tags.Explicitness}";
                    items.Add(new DslCompletionData(file, file, desc, "I", 0));
                }
                break;
        }
        return items;
    }

    [GeneratedRegex(@"^metronome-(\d+)bpm\.mp3$", RegexOptions.IgnoreCase)]
    private static partial Regex MetronomeFileRegex();

    private static IEnumerable<int> MetronomeBpms(AssetCatalog assets) =>
        assets.AudioFiles
            .Select(f => MetronomeFileRegex().Match(f))
            .Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value))
            .Distinct()
            .OrderBy(n => n);

    private static int CountLeadingSpaces(string s)
    {
        int i = 0;
        while (i < s.Length && s[i] == ' ') i++;
        return i;
    }
}

/// <summary>One completion item; <see cref="Complete"/> replaces the partial token and positions the caret.</summary>
internal sealed class DslCompletionData : ICompletionData
{
    private readonly string _insert;
    private readonly int _caretFromEnd;

    public DslCompletionData(string label, string insert, string description, string badge, int caretFromEnd)
    {
        Text = label;
        _insert = insert;
        _caretFromEnd = caretFromEnd;
        Description = description;
        Badge = badge;
    }

    public ImageSource? Image => null;
    public string Text { get; }
    public object Content => Text;
    public object Description { get; }
    public double Priority => 0;
    public string Badge { get; }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, _insert);
        if (_caretFromEnd > 0)
            textArea.Caret.Offset = Math.Max(textArea.Caret.Offset - _caretFromEnd, completionSegment.Offset);
    }
}
