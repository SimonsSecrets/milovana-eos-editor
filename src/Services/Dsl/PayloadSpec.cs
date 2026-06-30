namespace MilovanaEosEditor.Dsl;

/// <summary>What the <c>: payload</c> after a marker means — used for validation, colorizing, and
/// completion (e.g. a <see cref="NavTarget"/> payload offers page names; an <see cref="ImageRef"/>
/// offers buckets/filenames).</summary>
public enum PayloadKind
{
    None,
    PageKey,    // [PAGE: key]
    Html,       // [SAY: html] / [OPTION: label] — rendered text with limited inline tags
    ImageRef,   // [IMAGE: bucket/file.jpg | hold]
    NavTarget,  // [GOTO: key]
    Label,      // [NOTIFICATION: label] — plain button text
}

/// <summary>Declares the payload portion of a marker command.</summary>
public sealed class PayloadSpec
{
    public PayloadKind Kind { get; init; } = PayloadKind.None;
    public bool Required { get; init; }
    public string Description { get; init; } = "";

    public static readonly PayloadSpec None = new() { Kind = PayloadKind.None };

    public static PayloadSpec Of(PayloadKind kind, bool required, string description = "")
        => new() { Kind = kind, Required = required, Description = description };
}
