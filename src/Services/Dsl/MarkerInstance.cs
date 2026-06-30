namespace MilovanaEosEditor.Dsl;

/// <summary>One parsed <c>name=value</c> parameter, with document offsets so the editor can squiggle a
/// bad value or place the completion popup. Offsets are absolute character positions in the script text.</summary>
public sealed record ParamToken(string Name, string Value, int NameOffset, int ValueOffset, int ValueLength);

/// <summary>
/// A single parsed marker occurrence, e.g. <c>[SAY (align=center): Hello]</c>. Produced by the
/// <see cref="TeaseScriptParser"/> and consumed by validation, the page builder, the colorizer, and the
/// completion engine. Carries absolute document spans for the whole marker and its payload so problems
/// can be underlined precisely.
/// </summary>
public sealed class MarkerInstance
{
    public required string Keyword { get; init; }          // upper-cased, e.g. "SAY"
    public MarkerCommand? Command { get; init; }           // null when the keyword is unknown
    public required string RawParams { get; init; }        // text inside (...), "" if none
    public required IReadOnlyList<ParamToken> Params { get; init; }
    public required string Payload { get; init; }          // text after ": ", "" if none (raw, not trimmed)
    public int PayloadOffset { get; init; } = -1;          // absolute offset of payload start, -1 if absent
    public int PayloadLength { get; init; }
    public required int Line { get; init; }                // 1-based start line
    public required int StartOffset { get; init; }         // absolute offset of the opening '['
    public required int Length { get; init; }              // length of the whole marker text

    private Dictionary<string, ParamToken>? _byName;
    private Dictionary<string, ParamToken> ByName =>
        _byName ??= Params.GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.Last());

    public bool HasParam(string name) => ByName.ContainsKey(name);

    public string? Param(string name) => ByName.TryGetValue(name, out ParamToken? t) ? t.Value : null;

    public ParamToken? ParamToken(string name) => ByName.TryGetValue(name, out ParamToken? t) ? t : null;

    /// <summary>Trimmed payload (most keywords trim; SAY keeps its raw payload for spacing).</summary>
    public string TrimmedPayload => Payload.Trim();
}
