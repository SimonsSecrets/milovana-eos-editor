namespace MilovanaEosEditor.Dsl.Commands;

/// <summary><c>[PAUSE (secs=)]</c> — a silent timed block (no audio). Like METRONOME it makes its page
/// timed and becomes its own EOS page with a hidden timer / evenly-revealed says.</summary>
public sealed class PauseCommand : MarkerCommand
{
    public override string Keyword => "PAUSE";
    public override string Summary => "Silent timed block (no audio).";
    public override string CompletionSnippet => "PAUSE (secs=)";

    public override IReadOnlyList<ParameterSpec> Parameters { get; } = new[]
    {
        ParameterSpec.Double("secs", required: true, "Block duration in seconds (may be decimal)."),
    };
}
