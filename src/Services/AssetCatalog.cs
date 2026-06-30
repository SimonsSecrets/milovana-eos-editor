using System.IO;
using System.Text.Json;
using MilovanaEosEditor.Dsl;

namespace MilovanaEosEditor;

/// <summary>
/// In-memory view of a tease's assets, shared by the script compiler, autocomplete, the image picker,
/// and inline previews. Joins <c>asset-map.json</c> (locators, per-bucket file lists) with
/// <c>asset-content.json</c> (vision tags) and the on-disk <c>Gallery/&lt;bucket&gt;/</c> + <c>Files/</c>
/// folders. Everything is loaded once; call <see cref="Load"/> again to refresh after assets change.
/// </summary>
internal sealed class AssetCatalog : ILocatorSource
{
    private readonly string _teaseDir;
    private readonly Dictionary<string, string> _locators;            // "bucket/file" -> locator
    private readonly Dictionary<string, List<string>> _bucketFiles;   // bucket -> filenames (map order)
    private readonly Dictionary<string, ImageTags> _tags;             // "bucket/file" -> tags
    private readonly Dictionary<string, string?> _bucketThemes;       // bucket -> theme note
    private readonly HashSet<string> _audioFiles;                     // Files/ entries

    private AssetCatalog(
        string teaseDir,
        Dictionary<string, string> locators,
        Dictionary<string, List<string>> bucketFiles,
        Dictionary<string, ImageTags> tags,
        Dictionary<string, string?> bucketThemes,
        HashSet<string> audioFiles)
    {
        _teaseDir = teaseDir;
        _locators = locators;
        _bucketFiles = bucketFiles;
        _tags = tags;
        _bucketThemes = bucketThemes;
        _audioFiles = audioFiles;
    }

    public static AssetCatalog Load(string teaseDir)
    {
        var locators = new Dictionary<string, string>(StringComparer.Ordinal);
        var bucketFiles = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var tags = new Dictionary<string, ImageTags>(StringComparer.Ordinal);
        var themes = new Dictionary<string, string?>(StringComparer.Ordinal);
        var audio = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ---- asset-map.json: locators + bucket/file lists ----
        string mapPath = Path.Combine(teaseDir, "asset-map.json");
        if (File.Exists(mapPath))
        {
            try
            {
                AssetMap? map = JsonSerializer.Deserialize<AssetMap>(File.ReadAllText(mapPath), AssetMapGenerator.ReadOptions);
                if (map is not null)
                {
                    foreach ((string bucket, MapGallery gallery) in map.Galleries)
                    {
                        var files = new List<string>();
                        foreach ((string fileName, MapImage img) in gallery.Images)
                        {
                            locators[$"{bucket}/{fileName}"] = img.Locator;
                            files.Add(fileName);
                        }
                        bucketFiles[bucket] = files;
                    }
                }
            }
            catch (JsonException) { /* malformed map: treat as no locators */ }
        }

        // ---- asset-content.json: tags + bucket themes ----
        string contentPath = Path.Combine(teaseDir, "asset-content.json");
        if (File.Exists(contentPath))
        {
            try
            {
                AssetContent? content = JsonSerializer.Deserialize<AssetContent>(File.ReadAllText(contentPath), AssetMapGenerator.ReadOptions);
                if (content is not null)
                {
                    foreach ((string bucket, GalleryTags gallery) in content.Galleries)
                    {
                        themes[bucket] = gallery.Theme;
                        foreach ((string fileName, ImageTags imgTags) in gallery.Images)
                            tags[$"{bucket}/{fileName}"] = imgTags;
                    }
                }
            }
            catch (JsonException) { /* malformed content: treat as untagged */ }
        }

        // ---- Files/ listing (metronome clips etc.) ----
        string filesDir = Path.Combine(teaseDir, "Files");
        if (Directory.Exists(filesDir))
            foreach (string path in Directory.EnumerateFiles(filesDir))
                audio.Add(Path.GetFileName(path));

        return new AssetCatalog(teaseDir, locators, bucketFiles, tags, themes, audio);
    }

    // ---- ILocatorSource (compiler) ----
    public string? ResolveImageLocator(string bucketFile) => _locators.GetValueOrDefault(bucketFile);
    public bool AudioFilesKnown => _audioFiles.Count > 0;
    public bool HasAudioFile(string fileName) => _audioFiles.Contains(fileName);

    // ---- UI helpers (autocomplete / picker / inline preview) ----
    public IReadOnlyCollection<string> Buckets => _bucketFiles.Keys;
    public IReadOnlyCollection<string> AudioFiles => _audioFiles;

    public IReadOnlyList<string> FilesIn(string bucket) =>
        _bucketFiles.TryGetValue(bucket, out List<string>? files) ? files : Array.Empty<string>();

    public int ImageCountIn(string bucket) => _bucketFiles.TryGetValue(bucket, out List<string>? f) ? f.Count : 0;

    public string? ThemeOf(string bucket) => _bucketThemes.GetValueOrDefault(bucket);

    public ImageTags? TagsFor(string bucketFile) => _tags.GetValueOrDefault(bucketFile);

    public bool HasBucket(string bucket) => _bucketFiles.ContainsKey(bucket);

    /// <summary>Absolute path to the local image for a <c>bucket/file</c> reference, if present on disk.</summary>
    public string? ThumbnailPath(string bucketFile)
    {
        int slash = bucketFile.IndexOf('/');
        if (slash <= 0) return null;
        string bucket = bucketFile[..slash];
        string file = bucketFile[(slash + 1)..];
        string path = Path.Combine(_teaseDir, "Gallery", bucket, file);
        return File.Exists(path) ? path : null;
    }
}
