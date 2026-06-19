using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace AssetTagViewer;

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

/// <summary>A gallery folder node in the explorer tree: editable theme + its images.</summary>
internal sealed class GalleryNode : INotifyPropertyChanged
{
    private readonly GalleryTags _gallery;
    private bool _isExpanded = true; // expanded by default so image containers exist for selection sync
    private bool _isSelected;

    public GalleryNode(string name, GalleryTags gallery, IEnumerable<ImageItem> images)
    {
        Name = name;
        _gallery = gallery;
        Images = new ObservableCollection<ImageItem>(images);
    }

    public string Name { get; }
    public ObservableCollection<ImageItem> Images { get; }
    public string Header => $"{Name}  ({Images.Count})";

    /// <summary>Fired when theme/themeNote changes, so the window can mark itself dirty.</summary>
    public event Action? Edited;

    public string? Theme
    {
        get => _gallery.Theme;
        set { if (_gallery.Theme != value) { _gallery.Theme = value; RaiseEdited(); } }
    }

    public string? ThemeNote
    {
        get => _gallery.ThemeNote;
        set { if (_gallery.ThemeNote != value) { _gallery.ThemeNote = value; RaiseEdited(); } }
    }

    // UI-only state bound to TreeViewItem; never marks the document dirty.
    public bool IsExpanded { get => _isExpanded; set { if (_isExpanded != value) { _isExpanded = value; Notify(); } } }
    public bool IsSelected { get => _isSelected; set { if (_isSelected != value) { _isSelected = value; Notify(); } } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    private void RaiseEdited([CallerMemberName] string? n = null) { Notify(n); Edited?.Invoke(); }
}

/// <summary>
/// Bindable wrapper around one image's <see cref="ImageTags"/>. Tag setters write straight through
/// to the underlying model instance (the same object held by <see cref="AssetContent"/>), so saving
/// is just re-serializing the model. Tag changes raise <see cref="Edited"/> for dirty tracking;
/// UI-only state (selection) does not.
/// </summary>
internal sealed class ImageItem : INotifyPropertyChanged
{
    private readonly ImageTags _tags;
    private BitmapImage? _thumbnail;
    private bool _isSelected;

    public ImageItem(string gallery, string fileName, string? imagePath, ImageTags tags)
    {
        Gallery = gallery;
        FileName = fileName;
        ImagePath = imagePath;
        _tags = tags;
    }

    public string Gallery { get; }
    public string FileName { get; }
    public string? ImagePath { get; }
    public string Display => $"{Gallery}/{FileName}";
    public string TagSummary => $"{_tags.Subject} · pace {_tags.Pace} · {_tags.Explicitness}";

    // Lazily decoded small thumbnail for the gallery grid; the full-size preview is loaded separately.
    public BitmapImage? Thumbnail => _thumbnail ??= ImageLoader.Load(ImagePath, 160);

    public bool IsSelected { get => _isSelected; set { if (_isSelected != value) { _isSelected = value; Notify(); } } }

    public event Action? Edited;

    public string? Subject
    {
        get => _tags.Subject;
        set { if (_tags.Subject != value) { _tags.Subject = value; RaiseTag(); } }
    }

    public int Pace
    {
        get => _tags.Pace;
        set { if (_tags.Pace != value) { _tags.Pace = value; RaiseTag(); } }
    }

    public string? Explicitness
    {
        get => _tags.Explicitness;
        set { if (_tags.Explicitness != value) { _tags.Explicitness = value; RaiseTag(); } }
    }

    public string? Orientation
    {
        get => _tags.Orientation;
        set { if (_tags.Orientation != value) { _tags.Orientation = value; RaiseTag(); } }
    }

    public string? Notes
    {
        get => _tags.Notes;
        set { if (_tags.Notes != value) { _tags.Notes = value; RaiseTag(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    private void RaiseTag([CallerMemberName] string? name = null)
    {
        Notify(name);
        Notify(nameof(TagSummary)); // keep the thumbnail caption in sync
        Edited?.Invoke();
    }
}
