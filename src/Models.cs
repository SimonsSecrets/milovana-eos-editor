using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace MilovanaEosEditor;

// Mirrors asset-content.json (EOS-Tease-Authoring-Guide.md §5.4). Property order here is the
// serialized order, so it matches the hand-authored file. `vocabulary` is kept as a raw JsonNode
// so its free-form shape round-trips untouched even though this tool never edits it.
internal sealed class AssetContent
{
    public string? Tease { get; set; }
    public string? Description { get; set; }
    public JsonNode? Vocabulary { get; set; }
    public Dictionary<string, GalleryTags> Galleries { get; set; } = new();

    // Round-trip any unmodelled keys so saving can never silently drop data.
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

internal sealed class GalleryTags
{
    public string? Theme { get; set; }
    public string? ThemeNote { get; set; }
    public Dictionary<string, ImageTags> Images { get; set; } = new();

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

internal sealed class ImageTags
{
    public string? Subject { get; set; }
    public int Pace { get; set; }
    public string? Explicitness { get; set; }
    public string? Orientation { get; set; }
    public string? Notes { get; set; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Loads decoded, frozen bitmaps. OnLoad reads the whole file now so originals are never left locked.</summary>
internal static class ImageLoader
{
    public static BitmapImage? Load(string? path, int decodePixelWidth = 0)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        if (decodePixelWidth > 0) bmp.DecodePixelWidth = decodePixelWidth; // downscale thumbnails to save memory
        bmp.UriSource = new Uri(path);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
