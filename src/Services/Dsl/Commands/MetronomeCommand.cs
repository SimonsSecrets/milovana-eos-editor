namespace MilovanaEosEditor.Dsl.Commands;

/// <summary>
/// <c>[METRONOME (bpm=, secs=)]</c> — a timed block backed by a metronome clip. A page containing any
/// METRONOME/PAUSE is "timed": each block becomes its own EOS page (see the page builder). Emits an
/// <c>audio.play</c> sized to cover the block, then a hidden timer / evenly-revealed says.
/// </summary>
public sealed class MetronomeCommand : MarkerCommand
{
    public override string Keyword => "METRONOME";
    public override string Summary => "Timed block with a metronome clip (one tempo per page).";
    public override string CompletionSnippet => "METRONOME (bpm=, secs=)";

    public override IReadOnlyList<ParameterSpec> Parameters { get; } = new[]
    {
        ParameterSpec.Int("bpm", required: true, "Beats per minute (whole number); selects metronome-<bpm>bpm.mp3."),
        ParameterSpec.Double("secs", required: true, "Block duration in seconds (may be decimal)."),
    };

    /// <summary>The <c>audio.play</c> for this block; loops sized to cover <paramref name="secs"/>.</summary>
    public static EosObject BuildAudio(int bpm, double secs) =>
        EosActions.AudioPlay(EosActions.MetronomeFile(bpm), EosActions.LoopsForSeconds(secs));
}
