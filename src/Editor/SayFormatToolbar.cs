using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace MilovanaEosEditor.Editor;

/// <summary>
/// Text transforms for the floating say-formatting toolbar (the view hosts the popup; these methods do
/// the document edits). Bold/italic/underline/colour wrap the current selection in the inline tags
/// Milovana renders (<c>&lt;strong&gt; &lt;em&gt; &lt;u&gt; &lt;span style="color: #RRGGBB"&gt;</c>);
/// align sets the marker's <c>align=</c> parameter (left/center/right), per the authoring guide §3.1.
/// </summary>
internal static partial class SayFormatToolbar
{
    [GeneratedRegex(@"^\[SAY(\s*\(([^)]*)\))?", RegexOptions.IgnoreCase)]
    private static partial Regex SayHeadRegex();

    /// <summary>True when the caret sits on a <c>[SAY …]</c> marker line (so the toolbar should show).</summary>
    public static bool IsInSay(TextEditor editor)
    {
        DocumentLine line = editor.Document.GetLineByOffset(editor.CaretOffset);
        string text = editor.Document.GetText(line.Offset, line.Length);
        return SayHeadRegex().IsMatch(text);
    }

    public static void WrapBold(TextEditor editor) => Wrap(editor, "<strong>", "</strong>");
    public static void WrapItalic(TextEditor editor) => Wrap(editor, "<em>", "</em>");
    public static void WrapUnderline(TextEditor editor) => Wrap(editor, "<u>", "</u>");
    public static void WrapColor(TextEditor editor, string hex) => Wrap(editor, $"<span style=\"color: {hex}\">", "</span>");

    private static void Wrap(TextEditor editor, string open, string close)
    {
        TextDocument doc = editor.Document;
        int start = editor.SelectionLength > 0 ? editor.SelectionStart : editor.CaretOffset;
        int len = editor.SelectionLength;
        string selected = len > 0 ? doc.GetText(start, len) : "";
        string replacement = open + selected + close;
        doc.Replace(start, len, replacement);
        if (len == 0) editor.CaretOffset = start + open.Length; // caret between the tags
        else editor.Select(start, replacement.Length);
    }

    /// <summary>Set (or add) the SAY marker's <c>align=</c> parameter on the caret line.</summary>
    public static void SetAlign(TextEditor editor, string align)
    {
        TextDocument doc = editor.Document;
        DocumentLine line = doc.GetLineByOffset(editor.CaretOffset);
        string text = doc.GetText(line.Offset, line.Length);
        Match m = SayHeadRegex().Match(text);
        if (!m.Success) return;

        if (m.Groups[1].Success)
        {
            // Existing (...) — replace or append align in the parameter list.
            string prms = m.Groups[2].Value;
            int prmsStart = line.Offset + m.Groups[2].Index;
            List<string> parts = prms.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            bool replaced = false;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].StartsWith("align=", StringComparison.OrdinalIgnoreCase))
                {
                    parts[i] = "align=" + align;
                    replaced = true;
                }
            }
            if (!replaced) parts.Add("align=" + align);
            doc.Replace(prmsStart, prms.Length, string.Join(", ", parts));
        }
        else
        {
            // No parameters yet — insert "(align=…)" right after the SAY keyword.
            doc.Insert(line.Offset + 4, $" (align={align})");
        }
    }
}
