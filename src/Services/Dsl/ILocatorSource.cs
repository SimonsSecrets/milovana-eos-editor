namespace MilovanaEosEditor.Dsl;

/// <summary>
/// What the compiler needs from the asset side to resolve <c>[IMAGE]</c> locators and verify metronome
/// audio exists — mirrors the <c>asset-map.json</c> lookup and <c>Files/</c> listing used by
/// <c>Build-Tease.ps1</c>. Implemented by <c>AssetCatalog</c>; kept as an interface so the DSL layer has
/// no dependency on the app's services.
/// </summary>
public interface ILocatorSource
{
    /// <summary>Locator for a <c>bucket/filename.jpg</c> reference, or null if not in the asset map.</summary>
    string? ResolveImageLocator(string bucketFile);

    /// <summary>True when the <c>Files/</c> folder is populated (PS only checks audio when files exist).</summary>
    bool AudioFilesKnown { get; }

    /// <summary>True when <paramref name="fileName"/> (e.g. metronome-120bpm.mp3) is present in Files/.</summary>
    bool HasAudioFile(string fileName);
}
