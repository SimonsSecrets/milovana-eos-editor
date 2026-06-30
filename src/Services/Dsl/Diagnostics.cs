namespace MilovanaEosEditor.Dsl;

/// <summary>Severity of a <see cref="Diagnostic"/>. Errors abort an export; warnings do not.</summary>
public enum Severity
{
    Error,
    Warning,
}

/// <summary>
/// What kind of problem a <see cref="Diagnostic"/> describes. Drives the Problems-panel icon and lets
/// the editor offer kind-specific help (e.g. a "did you mean …?" page suggestion for a broken jump).
/// </summary>
public enum DiagnosticKind
{
    None,
    MarkerUnclosed,
    MarkerNotParsed,
    UnknownKeyword,
    OrphanBeforePage,
    UnknownParam,
    MalformedParam,
    MissingParam,
    BadParamType,
    EmptyPageKey,
    DuplicatePage,
    NoPages,
    NoStartPage,
    MissingPayload,
    BrokenJump,
    UnresolvedImage,
    MissingAudio,
    ChoiceNoOptions,
    SayBeforeBlock,
    UnsupportedSayTag,
    DeadEndPage,
    TimedNoExit,
}

/// <summary>
/// One problem found while parsing/validating/building a script. Mirrors the line-tied diagnostics of
/// <c>Build-Tease.ps1</c> but additionally carries an optional character span (<see cref="StartOffset"/>
/// + <see cref="Length"/>) so the editor can squiggle the exact offending token. A null span means the
/// problem is line-level only (still shown in the Problems list, just not underlined to a token).
/// </summary>
public sealed record Diagnostic(
    Severity Severity,
    DiagnosticKind Kind,
    int Line,
    string Message,
    int? StartOffset = null,
    int? Length = null);

/// <summary>Collects diagnostics during a compile. Convenience helpers mirror the PS <c>Err</c>/<c>Warn</c>.</summary>
public sealed class DiagnosticSink
{
    private readonly List<Diagnostic> _items = new();

    public IReadOnlyList<Diagnostic> Items => _items;
    public bool HasErrors => _items.Any(d => d.Severity == Severity.Error);
    public int ErrorCount => _items.Count(d => d.Severity == Severity.Error);
    public int WarningCount => _items.Count(d => d.Severity == Severity.Warning);

    public void Add(Diagnostic d) => _items.Add(d);

    public void Error(int line, DiagnosticKind kind, string message, int? start = null, int? length = null)
        => _items.Add(new Diagnostic(Severity.Error, kind, line, message, start, length));

    public void Warn(int line, DiagnosticKind kind, string message, int? start = null, int? length = null)
        => _items.Add(new Diagnostic(Severity.Warning, kind, line, message, start, length));
}
