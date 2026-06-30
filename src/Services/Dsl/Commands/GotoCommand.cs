namespace MilovanaEosEditor.Dsl.Commands;

/// <summary><c>[GOTO: key]</c> — jump to another page (a target equal to the page's own key is an
/// intentional loop). Target resolution is validated by the compiler against all declared pages.</summary>
public sealed class GotoCommand : MarkerCommand
{
    public override string Keyword => "GOTO";
    public override string Summary => "Jump to another page.";
    public override string CompletionSnippet => "GOTO: ";
    public override PayloadSpec Payload => PayloadSpec.Of(PayloadKind.NavTarget, required: true, "Destination page key.");
}
