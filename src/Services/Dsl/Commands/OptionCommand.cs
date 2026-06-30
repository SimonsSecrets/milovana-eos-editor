namespace MilovanaEosEditor.Dsl.Commands;

/// <summary><c>[OPTION (target=, color=#hex): label]</c> — one button of the preceding
/// <c>[CHOICE]</c>. Label/target emptiness is validated by the compiler when it assembles the choice.</summary>
public sealed class OptionCommand : MarkerCommand
{
    public override string Keyword => "OPTION";
    public override string Summary => "A button of the preceding [CHOICE].";
    public override string CompletionSnippet => "OPTION (target=): ";

    public override IReadOnlyList<ParameterSpec> Parameters { get; } = new[]
    {
        ParameterSpec.String("target", required: true, "Destination page key."),
        ParameterSpec.HexColor("color", required: false, "Button colour, e.g. #1976d2."),
    };

    public override PayloadSpec Payload => PayloadSpec.Of(PayloadKind.Html, required: true, "Button label text.");
}
