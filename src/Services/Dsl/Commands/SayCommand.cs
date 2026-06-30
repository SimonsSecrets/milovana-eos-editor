using System.Text.RegularExpressions;

namespace MilovanaEosEditor.Dsl.Commands;

/// <summary>
/// <c>[SAY (mode=, align=, duration=): html]</c> — show text. Params and payload are optional; the
/// payload is wrapped in <c>&lt;p&gt;…&lt;/p&gt;</c> and may use a small set of inline tags. The
/// effective <c>mode</c> when omitted (instant vs. pause for the last say on a goto-exit page) is decided
/// by the page builder, so <see cref="BuildAction"/> takes the resolved mode.
/// </summary>
public sealed partial class SayCommand : MarkerCommand
{
    public override string Keyword => "SAY";
    public override string Summary => "Show text (params optional; text auto-wrapped in <p>…</p>).";
    public override string CompletionSnippet => "SAY: ";

    public override IReadOnlyList<ParameterSpec> Parameters { get; } = new[]
    {
        ParameterSpec.Enum("mode", required: false,
            "Timing. Omit for Auto; pause = wait for click; instant = proceed; autoplay = pause ~reading time; custom = pause 'duration'.",
            "pause", "instant", "autoplay", "custom"),
        ParameterSpec.Enum("align", required: false, "Paragraph alignment (defaults to center).",
            "left", "center", "right"),
        ParameterSpec.String("duration", required: false, "Only with mode=custom, e.g. '3s'."),
    };

    public override PayloadSpec Payload => PayloadSpec.Of(PayloadKind.Html, required: false,
        "Text. Inline tags allowed: <strong> <em> <u> <span style=\"color: #RRGGBB\">.");

    // Tags Milovana actually renders; anything else is silently dropped on Milovana, so flag it.
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
        { "p", "br", "strong", "em", "u", "span" };

    [GeneratedRegex(@"</?\s*([A-Za-z][A-Za-z0-9]*)")]
    private static partial Regex TagRegex();

    protected override void ValidateSelf(MarkerInstance m, DiagnosticSink diags)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match mm in TagRegex().Matches(m.Payload))
        {
            string tag = mm.Groups[1].Value;
            if (!AllowedTags.Contains(tag) && seen.Add(tag))
                diags.Warn(m.Line, DiagnosticKind.UnsupportedSayTag,
                    $"[SAY] uses tag '<{tag}>' which Milovana does not render (only <strong>/<em>/<u>/<a> and " +
                    "<span style=\"color: #RRGGBB\">). For colored text use <span style=\"color: #RRGGBB\">text</span>.");
        }
    }

    /// <summary>Build the <c>say</c> action from an already space-normalized payload + resolved fields.</summary>
    public static EosObject BuildAction(string normalizedPayload, string? mode, string? align, string? duration)
    {
        var say = new EosObject().Add("label", new EosString("<p>" + normalizedPayload + "</p>"));
        if (!string.IsNullOrEmpty(mode)) say.Add("mode", new EosString(mode));
        if (!string.IsNullOrEmpty(align)) say.Add("align", new EosString(align));
        if (!string.IsNullOrEmpty(duration)) say.Add("duration", new EosString(duration));
        return new EosObject().Add("say", say);
    }
}
