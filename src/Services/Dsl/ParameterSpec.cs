namespace MilovanaEosEditor.Dsl;

/// <summary>The value domain of a marker parameter — drives both validation and autocomplete.</summary>
public enum ParamValueKind
{
    String,
    Int,
    Double,
    Enum,
    HexColor,
}

/// <summary>
/// Declares one parameter of a marker command (name, whether it is required, its value domain, and a
/// short description). A command lists these once; the base <see cref="MarkerCommand.Validate"/> turns
/// them into the unknown/malformed/missing/typed checks, and the completion engine turns them into
/// parameter-name and value suggestions — so there is a single source of truth per parameter.
/// </summary>
public sealed class ParameterSpec
{
    public required string Name { get; init; }
    public bool Required { get; init; }
    public ParamValueKind Kind { get; init; } = ParamValueKind.String;

    /// <summary>Allowed values when <see cref="Kind"/> is <see cref="ParamValueKind.Enum"/>.</summary>
    public IReadOnlyList<string> EnumValues { get; init; } = Array.Empty<string>();

    public string Description { get; init; } = "";

    public static ParameterSpec Enum(string name, bool required, string description, params string[] values)
        => new() { Name = name, Required = required, Kind = ParamValueKind.Enum, EnumValues = values, Description = description };

    public static ParameterSpec Int(string name, bool required, string description)
        => new() { Name = name, Required = required, Kind = ParamValueKind.Int, Description = description };

    public static ParameterSpec Double(string name, bool required, string description)
        => new() { Name = name, Required = required, Kind = ParamValueKind.Double, Description = description };

    public static ParameterSpec String(string name, bool required, string description)
        => new() { Name = name, Required = required, Kind = ParamValueKind.String, Description = description };

    public static ParameterSpec HexColor(string name, bool required, string description)
        => new() { Name = name, Required = required, Kind = ParamValueKind.HexColor, Description = description };
}
