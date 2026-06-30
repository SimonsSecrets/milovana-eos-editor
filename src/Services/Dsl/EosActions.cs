using System.Globalization;

namespace MilovanaEosEditor.Dsl;

/// <summary>
/// Builders for EOS action objects that are not 1:1 with a marker — the derived <c>timer</c> a timed
/// block emits, the <c>goto</c>/<c>end</c> page exits, the auto-injected <c>notification.remove</c>, and
/// the <c>audio.play</c> shared by <c>[AUDIO]</c> and <c>[METRONOME]</c>. Key insertion order matches
/// <c>Build-Tease.ps1</c> exactly so the serialized JSON is byte-identical.
/// </summary>
public static class EosActions
{
    /// <summary>Metronome clip filename for a BPM, e.g. 60 → <c>metronome-060bpm.mp3</c>.</summary>
    public static string MetronomeFile(int bpm) =>
        "metronome-" + bpm.ToString("000", CultureInfo.InvariantCulture) + "bpm.mp3";

    public static EosObject Timer(string duration, string style) =>
        new EosObject().Add("timer", new EosObject().Add("duration", new EosString(duration)).Add("style", new EosString(style)));

    public static EosObject Goto(string target) =>
        new EosObject().Add("goto", new EosObject().Add("target", new EosString(target)));

    public static EosObject End() =>
        new EosObject().Add("end", new EosObject());

    public static EosObject NotificationRemove(string id) =>
        new EosObject().Add("notification.remove", new EosObject().Add("id", new EosString(id)));

    /// <summary><c>audio.play</c> with the PS field order: locator, volume, loops, background.</summary>
    public static EosObject AudioPlay(string file, int loops) =>
        new EosObject().Add("audio.play", new EosObject()
            .Add("locator", new EosString("file:" + file))
            .Add("volume", EosNumber.Double(1.0))
            .Add("loops", EosNumber.Int(loops))
            .Add("background", new EosBool(false)));

    /// <summary>Loop count for a metronome covering <paramref name="secs"/>: ceil(secs/3)+1 (PS aAudio).</summary>
    public static int LoopsForSeconds(double secs) => (int)(Math.Ceiling(secs / 3.0) + 1);
}
