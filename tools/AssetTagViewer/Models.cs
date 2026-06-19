using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AssetTagViewer;

// Mirrors asset-content.json (EOS-Tease-Authoring-Guide.md §5.3). Property order here is the
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

/// <summary>
/// Bindable wrapper around one image's <see cref="ImageTags"/>. Setters write straight through to
/// the underlying model instance (which is the same object held by <see cref="AssetContent"/>), so
/// saving is just re-serializing the model. Raises <see cref="Edited"/> on any change for dirty
/// tracking.
/// </summary>
internal sealed class ImageItem : INotifyPropertyChanged
{
    private readonly ImageTags _tags;

    public ImageItem(string gallery, string fileName, string? imagePath, ImageTags tags,
                     string? theme, string? themeNote)
    {
        Gallery = gallery;
        FileName = fileName;
        ImagePath = imagePath;
        _tags = tags;
        Theme = theme;
        ThemeNote = themeNote;
    }

    public string Gallery { get; }
    public string FileName { get; }
    public string? ImagePath { get; }
    public string? Theme { get; }
    public string? ThemeNote { get; }
    public string Display => $"{Gallery}/{FileName}";

    /// <summary>Fired whenever any tag value changes, so the window can mark itself dirty.</summary>
    public event Action? Edited;

    public string? Subject
    {
        get => _tags.Subject;
        set { if (_tags.Subject != value) { _tags.Subject = value; Raise(); } }
    }

    public int Pace
    {
        get => _tags.Pace;
        set { if (_tags.Pace != value) { _tags.Pace = value; Raise(); } }
    }

    public string? Explicitness
    {
        get => _tags.Explicitness;
        set { if (_tags.Explicitness != value) { _tags.Explicitness = value; Raise(); } }
    }

    public string? Orientation
    {
        get => _tags.Orientation;
        set { if (_tags.Orientation != value) { _tags.Orientation = value; Raise(); } }
    }

    public string? Notes
    {
        get => _tags.Notes;
        set { if (_tags.Notes != value) { _tags.Notes = value; Raise(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        Edited?.Invoke();
    }
}
