using System.Windows;
using System.Windows.Media;

namespace MilovanaEosEditor.Editor;

/// <summary>
/// Frozen brushes for DSL syntax highlighting and squiggles, resolved from the keys in
/// <c>Themes/Editor.xaml</c> (the single source of colour truth) with hard-coded fallbacks matching the
/// design mockup, so the colorizer never depends on resource-lookup timing.
/// </summary>
internal static class EditorColors
{
    public static Brush Keyword { get; } = Resolve("SyntaxKeywordBrush", "#0b6bcb");
    public static Brush Page { get; } = Resolve("SyntaxPageBrush", "#a626a4");
    public static Brush ParamName { get; } = Resolve("SyntaxParamNameBrush", "#267f99");
    public static Brush ParamValue { get; } = Resolve("SyntaxParamValueBrush", "#0a7c46");
    public static Brush Number { get; } = Resolve("SyntaxNumberBrush", "#098658");
    public static Brush NavTarget { get; } = Resolve("SyntaxNavTargetBrush", "#a05a00");
    public static Brush Punctuation { get; } = Resolve("SyntaxPunctuationBrush", "#888888");
    public static Brush SayText { get; } = Resolve("SyntaxSayTextBrush", "#444444");
    public static Brush ColorLiteral { get; } = Resolve("SyntaxColorLiteralBrush", "#a05a00");
    public static Brush LineNumber { get; } = Resolve("LineNumberBrush", "#c4c4c4");
    public static Brush Error { get; } = Resolve("EditorErrorBrush", "#c42b1c");
    public static Brush Warning { get; } = Resolve("EditorWarningBrush", "#e8a33d");

    private static Brush Resolve(string key, string fallbackHex)
    {
        if (Application.Current?.TryFindResource(key) is Brush b) return b;
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex));
        brush.Freeze();
        return brush;
    }
}
