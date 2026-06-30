namespace MilovanaEosEditor.Dsl.Commands;

/// <summary>
/// <c>[AUDIO (bpm=, loops=)]</c> — a non-blocking looping metronome on a non-timed page. Unlike
/// <c>[METRONOME]</c> it does not split the page; it just emits an <c>audio.play</c> that loops
/// <c>loops</c> times.
/// </summary>
public sealed class AudioCommand : MarkerCommand
{
    public override string Keyword => "AUDIO";
    public override string Summary => "Non-blocking looping metronome on a normal page.";
    public override string CompletionSnippet => "AUDIO (bpm=, loops=)";

    public override IReadOnlyList<ParameterSpec> Parameters { get; } = new[]
    {
        ParameterSpec.Int("bpm", required: true, "Beats per minute (whole number); selects metronome-<bpm>bpm.mp3."),
        ParameterSpec.Int("loops", required: true, "Total play count (e.g. 2 plays twice)."),
    };

    public static EosObject BuildAction(int bpm, int loops) =>
        EosActions.AudioPlay(EosActions.MetronomeFile(bpm), loops);
}
