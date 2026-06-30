namespace MilovanaEosEditor.Dsl.Commands;

/// <summary><c>[END]</c> — terminate the tease (the player is prompted to rate).</summary>
public sealed class EndCommand : MarkerCommand
{
    public override string Keyword => "END";
    public override string Summary => "End the tease (prompts the player to rate).";
    public override string CompletionSnippet => "END";
}
