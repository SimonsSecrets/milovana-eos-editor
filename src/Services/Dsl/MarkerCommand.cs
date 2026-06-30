using System.Globalization;

namespace MilovanaEosEditor.Dsl;

/// <summary>
/// Base class for a single DSL marker (one keyword such as <c>SAY</c> or <c>IMAGE</c>). Each concrete
/// command is fully self-describing — it declares its <see cref="Keyword"/>, its
/// <see cref="Parameters"/> (names + value domains), and its <see cref="Payload"/> — so adding a future
/// command is just "add a subclass and register it" (see <see cref="CommandRegistry"/>); nothing in the
/// parser, validator, colorizer, or completion engine needs to change.
///
/// <para>Validation is split deliberately: the <see cref="Validate"/> here covers everything decidable
/// from the marker alone (unknown/malformed/missing/typed parameters, plus a per-command
/// <see cref="ValidateSelf"/> hook). Cross-marker checks that need the whole script — nav-target
/// resolution, duplicate pages, "[CHOICE] has no [OPTION]", timed-page ordering, image/audio locator
/// resolution — live in <c>TeaseScriptCompiler</c>/<c>PageBuilder</c>, mirroring how
/// <c>Build-Tease.ps1</c> reports them during assembly.</para>
/// </summary>
public abstract class MarkerCommand
{
    /// <summary>Upper-cased keyword, e.g. <c>"SAY"</c>.</summary>
    public abstract string Keyword { get; }

    /// <summary>One-line description shown in the completion popup.</summary>
    public abstract string Summary { get; }

    /// <summary>A code snippet inserted when the keyword is accepted from completion (without brackets).</summary>
    public virtual string CompletionSnippet => Keyword;

    public virtual IReadOnlyList<ParameterSpec> Parameters => Array.Empty<ParameterSpec>();

    public virtual PayloadSpec Payload => PayloadSpec.None;

    /// <summary>Per-marker validation decidable without the rest of the script.</summary>
    public void Validate(MarkerInstance m, DiagnosticSink diags)
    {
        ValidateMalformedParams(m, diags);   // always, even for paramless keywords (matches PS)
        if (Parameters.Count > 0)
        {
            ValidateUnknownParams(m, diags);
            ValidateParamValues(m, diags);
        }
        ValidateSelf(m, diags);
    }

    /// <summary>Override for command-specific marker-local checks (e.g. SAY's unsupported-tag warning).</summary>
    protected virtual void ValidateSelf(MarkerInstance m, DiagnosticSink diags) { }

    // A parameter segment with no '=' (e.g. the typo "mode-pause") is silently dropped by the parser, so
    // the intended param would vanish with no effect; flag it from the RAW param text. (PS: WarnMalformedParams)
    private void ValidateMalformedParams(MarkerInstance m, DiagnosticSink diags)
    {
        if (string.IsNullOrWhiteSpace(m.RawParams)) return;
        foreach (string seg in m.RawParams.Split(','))
        {
            string s = seg.Trim();
            if (s.Length > 0 && !s.Contains('='))
                diags.Warn(m.Line, DiagnosticKind.MalformedParam,
                    $"[{Keyword}] malformed parameter '{s}' (expected name=value, e.g. 'mode=pause'); it was ignored");
        }
    }

    private void ValidateUnknownParams(MarkerInstance m, DiagnosticSink diags)
    {
        var allowed = Parameters.Select(p => p.Name).ToHashSet();
        foreach (ParamToken p in m.Params)
        {
            if (!allowed.Contains(p.Name))
                diags.Warn(m.Line, DiagnosticKind.UnknownParam,
                    $"[{Keyword}] unknown parameter '{p.Name}=' (expected one of: {string.Join(", ", allowed)})",
                    p.NameOffset, p.Name.Length);
        }
    }

    private void ValidateParamValues(MarkerInstance m, DiagnosticSink diags)
    {
        foreach (ParameterSpec spec in Parameters)
        {
            ParamToken? token = m.ParamToken(spec.Name);
            if (token is null)
            {
                if (spec.Required)
                    diags.Error(m.Line, DiagnosticKind.MissingParam,
                        $"[{Keyword}] missing required parameter '{spec.Name}='");
                continue;
            }

            string value = token.Value;
            switch (spec.Kind)
            {
                case ParamValueKind.Int when !int.TryParse(value, out _):
                    diags.Error(m.Line, DiagnosticKind.BadParamType,
                        $"[{Keyword}] parameter '{spec.Name}=' is not a whole number: '{value}'",
                        token.ValueOffset, token.ValueLength);
                    break;
                case ParamValueKind.Double when !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _):
                    diags.Error(m.Line, DiagnosticKind.BadParamType,
                        $"[{Keyword}] parameter '{spec.Name}=' is not a number: '{value}'",
                        token.ValueOffset, token.ValueLength);
                    break;
                case ParamValueKind.Enum when !spec.EnumValues.Contains(value):
                    diags.Warn(m.Line, DiagnosticKind.BadParamType,
                        $"[{Keyword}] parameter '{spec.Name}=' should be one of: {string.Join(", ", spec.EnumValues)} (got '{value}')",
                        token.ValueOffset, token.ValueLength);
                    break;
                case ParamValueKind.HexColor when !IsHexColor(value):
                    diags.Warn(m.Line, DiagnosticKind.BadParamType,
                        $"[{Keyword}] parameter '{spec.Name}=' should be a hex colour like #1976d2 (got '{value}')",
                        token.ValueOffset, token.ValueLength);
                    break;
            }
        }
    }

    private static bool IsHexColor(string v) =>
        v.Length is 4 or 7 && v[0] == '#' && v.Skip(1).All(Uri.IsHexDigit);

    // ---- emit helpers shared by subclasses ----
    protected static int? TryInt(MarkerInstance m, string name) =>
        int.TryParse(m.Param(name), out int v) ? v : null;

    protected static double? TryDouble(MarkerInstance m, string name) =>
        double.TryParse(m.Param(name), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : null;
}
