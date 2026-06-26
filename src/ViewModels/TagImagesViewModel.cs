using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MilovanaEosEditor;

/// <summary>Sort order for the images shown in a bucket's grid.</summary>
public enum ImageSort
{
    Name,         // filename A→Z (default)
    Pace,         // pace 1→5, untagged last
    Explicitness, // clothed→explicit (vocabulary order), untagged last
}

/// <summary>
/// The <b>Tag Images</b> tool (authoring steps 7–8): browse the tease's themed buckets, view each
/// image, and tag it (subject / pace / explicitness / notes). Edits write straight through to the
/// shared <see cref="ImageTags"/> instances inside <see cref="AssetContent"/> and are
/// <b>auto-saved</b> (debounced) to <c>asset-content.json</c> — there is no Save button, matching
/// the mockup's "Auto-saved" indicator.
/// </summary>
public sealed partial class TagImagesViewModel : ToolViewModel
{
    // Same JSON shape the generator uses, so asset-content.json stays byte-consistent on save.
    private static readonly JsonSerializerOptions ReadOptions = AssetMapGenerator.ReadOptions;
    private static readonly JsonSerializerOptions WriteOptions = AssetMapGenerator.WriteOptions;

    private static readonly string[] SubjectFallback = { "solo", "machine", "partner" };
    private static readonly string[] ExplicitFallback = { "clothed", "underwear", "partial-nudity", "nude", "explicit" };

    // Guide-anchored BPM bands (EOS-Tease-Authoring-Guide.md §5.3), not the mockup's labels.
    private static readonly (int N, string Bpm, string Label)[] PaceBands =
    {
        (1, "≤40", "Still / posed"),
        (2, "40–70", "Slow"),
        (3, "70–110", "Moderate"),
        (4, "110–150", "Fast"),
        (5, "150+", "Frantic / hard"),
    };

    private readonly string? _contentPath;
    private readonly AssetContent? _content;
    private readonly DispatcherTimer _saveTimer;
    private string[] _explicitOrder = ExplicitFallback;

    public TagImagesViewModel(TeaseInfo tease) : base(tease)
    {
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += (_, _) => FlushSave();

        Buckets = new ObservableCollection<TagBucket>();
        SubjectChips = new ObservableCollection<ChipOption>();
        ExplicitChips = new ObservableCollection<ChipOption>();
        PaceOptions = new ObservableCollection<PaceOption>();

        string path = Path.Combine(tease.FolderPath, "asset-content.json");
        if (File.Exists(path))
        {
            try
            {
                _content = JsonSerializer.Deserialize<AssetContent>(File.ReadAllText(path), ReadOptions);
                if (_content is not null)
                {
                    _contentPath = path;
                    BuildBuckets();
                    BuildVocabularyOptions();
                }
            }
            catch
            {
                _content = null; // fall through to the empty state rather than crash
            }
        }

        if (Buckets.Count > 0) SelectedBucket = Buckets[0];
    }

    public override string Title => "Tag Images";
    public override string Subtitle => "Review galleries and tag each image (subject, pace, explicitness).";

    public ObservableCollection<TagBucket> Buckets { get; }
    public ObservableCollection<ChipOption> SubjectChips { get; }
    public ObservableCollection<ChipOption> ExplicitChips { get; }
    public ObservableCollection<PaceOption> PaceOptions { get; }

    /// <summary>The selected bucket's images, sorted by <see cref="SelectedSort"/>. The grid binds here.</summary>
    public ObservableCollection<TagImage> GridImages { get; } = new();

    /// <summary>False → the page shows the empty "run Generate the asset map first" state.</summary>
    public bool HasContent => Buckets.Count > 0;

    [ObservableProperty] private TagBucket? _selectedBucket;
    [ObservableProperty] private TagImage? _selectedImage;
    [ObservableProperty] private string _saveState = "Auto-saved";
    [ObservableProperty] private ImageSort _selectedSort = ImageSort.Name;

    public bool HasSelectedImage => SelectedImage is not null;

    /// <summary>Pace-name hint shown under the pace buttons for the selected image.</summary>
    public string PaceHint => SelectedImage is { Pace: > 0 } img
        ? PaceBands.FirstOrDefault(b => b.N == img.Pace).Label ?? string.Empty
        : string.Empty;

    // ---- selection ----

    [RelayCommand]
    private void SelectBucket(TagBucket bucket) => SelectedBucket = bucket;

    [RelayCommand]
    private void SelectImage(TagImage image) => SelectedImage = image;

    partial void OnSelectedBucketChanged(TagBucket? oldValue, TagBucket? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;

        RebuildGrid();

        // Land on the first untagged image (the ones needing attention), else the first.
        SelectedImage = newValue is null
            ? null
            : newValue.Images.FirstOrDefault(i => !i.IsTagged) ?? newValue.Images.FirstOrDefault();
    }

    partial void OnSelectedSortChanged(ImageSort value) => RebuildGrid();

    /// <summary>
    /// Rebuild the grid's image list for the current bucket + sort. Sorting only happens on a
    /// bucket/sort change (not on every tag edit) so a card never jumps out from under the author
    /// mid-tagging. Untagged images (pace 0 / blank explicitness) sort to the end of those orders.
    /// </summary>
    private void RebuildGrid()
    {
        GridImages.Clear();
        if (SelectedBucket is null) return;

        IEnumerable<TagImage> sorted = SelectedSort switch
        {
            ImageSort.Pace => SelectedBucket.Images
                .OrderBy(i => i.Pace > 0 ? i.Pace : int.MaxValue)
                .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase),
            ImageSort.Explicitness => SelectedBucket.Images
                .OrderBy(i => ExplicitIndex(i.Explicitness))
                .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase),
            _ => SelectedBucket.Images.OrderBy(i => i.FileName, StringComparer.OrdinalIgnoreCase),
        };

        foreach (TagImage image in sorted) GridImages.Add(image);
    }

    private int ExplicitIndex(string? value)
    {
        if (string.IsNullOrEmpty(value)) return int.MaxValue;
        int idx = Array.IndexOf(_explicitOrder, value);
        return idx >= 0 ? idx : int.MaxValue - 1; // unknown values just before the blank ones
    }

    partial void OnSelectedImageChanged(TagImage? oldValue, TagImage? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
        RefreshOptionSelection();
        OnPropertyChanged(nameof(HasSelectedImage));
        OnPropertyChanged(nameof(PaceHint));
    }

    // ---- tag edits (called by the chip / pace option commands) ----

    private void SetSubject(string value)
    {
        if (SelectedImage is null) return;
        SelectedImage.Subject = value;
        AfterEdit();
    }

    private void SetExplicitness(string value)
    {
        if (SelectedImage is null) return;
        SelectedImage.Explicitness = value;
        AfterEdit();
    }

    private void SetPace(int value)
    {
        if (SelectedImage is null) return;
        SelectedImage.Pace = value;
        AfterEdit();
        OnPropertyChanged(nameof(PaceHint));
    }

    /// <summary>Recompute selection + bucket coverage after a chip/pace edit, then queue a save.</summary>
    private void AfterEdit()
    {
        RefreshOptionSelection();
        SelectedBucket?.Recount();
        QueueSave();
    }

    // ---- building ----

    private void BuildBuckets()
    {
        foreach ((string galleryName, GalleryTags gallery) in _content!.Galleries)
        {
            var images = new List<TagImage>();
            foreach ((string fileName, ImageTags tags) in gallery.Images)
            {
                string? imagePath = ResolveImagePath(Tease.FolderPath, galleryName, fileName);
                var image = new TagImage(galleryName, fileName, imagePath, tags);
                image.Edited += OnImageEdited; // notes typing etc. coalesce into a debounced save
                images.Add(image);
            }

            // Short mono name = the gallery key; the descriptive note = themeNote ?? theme.
            string note = gallery.ThemeNote ?? gallery.Theme ?? string.Empty;
            Buckets.Add(new TagBucket(galleryName, note, images));
        }
    }

    private void BuildVocabularyOptions()
    {
        foreach (string v in Vocab("subject", SubjectFallback))
            SubjectChips.Add(new ChipOption(v, () => SetSubject(v)));

        _explicitOrder = Vocab("explicitness", ExplicitFallback); // also the Explicitness sort order
        foreach (string v in _explicitOrder)
            ExplicitChips.Add(new ChipOption(v, () => SetExplicitness(v)));
        foreach ((int n, string bpm, _) in PaceBands)
            PaceOptions.Add(new PaceOption(n, bpm, () => SetPace(n)));
    }

    /// <summary>Drive the chip lists from the file's own <c>vocabulary</c> block when present.</summary>
    private string[] Vocab(string key, string[] fallback)
    {
        if (_content?.Vocabulary is JsonObject vo &&
            vo.TryGetPropertyValue(key, out JsonNode? node) && node is JsonArray arr)
        {
            var values = arr.Where(n => n is not null).Select(n => n!.GetValue<string>()).ToArray();
            if (values.Length > 0) return values;
        }
        return fallback;
    }

    private void RefreshOptionSelection()
    {
        TagImage? img = SelectedImage;
        foreach (ChipOption c in SubjectChips) c.IsSelected = img is not null && c.Label == img.Subject;
        foreach (ChipOption c in ExplicitChips) c.IsSelected = img is not null && c.Label == img.Explicitness;
        foreach (PaceOption p in PaceOptions) p.IsSelected = img is not null && p.Number == img.Pace;
    }

    /// <summary>
    /// Resolve a local image: the documented layout (<c>Gallery/&lt;name&gt;/&lt;file&gt;</c>) first,
    /// then a recursive filename fallback so the tool still works if the folder layout differs.
    /// </summary>
    private static string? ResolveImagePath(string teaseDir, string gallery, string fileName)
    {
        string primary = Path.Combine(teaseDir, "Gallery", gallery, fileName);
        if (File.Exists(primary)) return primary;
        try
        {
            return Directory.EnumerateFiles(teaseDir, fileName, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    // ---- auto-save (debounced) ----

    private void OnImageEdited()
    {
        SelectedBucket?.Recount();
        QueueSave();
    }

    private void QueueSave()
    {
        if (_contentPath is null) return;
        SaveState = "Saving…";
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void FlushSave()
    {
        _saveTimer.Stop();
        if (_content is null || _contentPath is null) return;
        try
        {
            File.WriteAllText(_contentPath, JsonSerializer.Serialize(_content, WriteOptions) + "\n");
            SaveState = "Auto-saved";
        }
        catch
        {
            SaveState = "Save failed";
        }
    }
}

/// <summary>One themed gallery in the bucket list: its images plus live tagged-coverage stats.</summary>
public sealed partial class TagBucket : ObservableObject
{
    public TagBucket(string name, string note, IEnumerable<TagImage> images)
    {
        Name = name;
        Note = note;
        Images = new ObservableCollection<TagImage>(images);
        Count = Images.Count;
        _tagged = Images.Count(i => i.IsTagged);
    }

    public string Name { get; }
    public string Note { get; }
    public int Count { get; }
    public ObservableCollection<TagImage> Images { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int _tagged;

    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
    public string CountLabel => $"{Tagged}/{Count}";
    public bool IsFullyTagged => Count > 0 && Tagged == Count;
    public string ImagesLabel => $"{Count} image{(Count == 1 ? "" : "s")}";

    public void Recount()
    {
        Tagged = Images.Count(i => i.IsTagged);
        OnPropertyChanged(nameof(CountLabel));
        OnPropertyChanged(nameof(IsFullyTagged));
    }
}

/// <summary>
/// Bindable wrapper around one image's <see cref="ImageTags"/>. Tag setters write straight through to
/// the underlying model (the same object held by <see cref="AssetContent"/>), so saving is just
/// re-serializing. Tag changes raise <see cref="Edited"/> for the debounced auto-save.
/// </summary>
public sealed partial class TagImage : ObservableObject
{
    private readonly ImageTags _tags;
    private BitmapImage? _thumbnail;
    private BitmapImage? _preview;
    private bool _thumbLoaded;
    private bool _previewLoaded;

    internal TagImage(string gallery, string fileName, string? imagePath, ImageTags tags)
    {
        Gallery = gallery;
        FileName = fileName;
        ImagePath = imagePath;
        _tags = tags;
    }

    public string Gallery { get; }
    public string FileName { get; }
    public string? ImagePath { get; }
    public bool IsMissing => ImagePath is null;

    // Lazily decoded: a small grid thumbnail and a larger editor preview.
    public BitmapImage? Thumbnail
    {
        get { if (!_thumbLoaded) { _thumbnail = ImageLoader.Load(ImagePath, 200); _thumbLoaded = true; } return _thumbnail; }
    }

    public BitmapImage? Preview
    {
        get { if (!_previewLoaded) { _preview = ImageLoader.Load(ImagePath, 1400); _previewLoaded = true; } return _preview; }
    }

    [ObservableProperty] private bool _isSelected;

    /// <summary>Tagged once it has a subject and a real pace (the step-6 stub is blank/pace 0).</summary>
    public bool IsTagged => _tags.Pace > 0 && !string.IsNullOrWhiteSpace(_tags.Subject);

    public string PaceLabel => $"p{_tags.Pace}";

    public event Action? Edited;

    public string? Subject
    {
        get => _tags.Subject;
        set { if (_tags.Subject != value) { _tags.Subject = value; RaiseTag(); } }
    }

    public int Pace
    {
        get => _tags.Pace;
        set { if (_tags.Pace != value) { _tags.Pace = value; RaiseTag(nameof(PaceLabel)); } }
    }

    public string? Explicitness
    {
        get => _tags.Explicitness;
        set { if (_tags.Explicitness != value) { _tags.Explicitness = value; RaiseTag(); } }
    }

    public string? Notes
    {
        get => _tags.Notes;
        set { if (_tags.Notes != value) { _tags.Notes = value; RaiseTag(); } }
    }

    private void RaiseTag(string? extra = null)
    {
        if (extra is not null) OnPropertyChanged(extra);
        OnPropertyChanged(nameof(IsTagged));
        Edited?.Invoke();
    }
}

/// <summary>A selectable vocabulary chip (subject / explicitness).</summary>
public sealed partial class ChipOption : ObservableObject
{
    public ChipOption(string label, Action onSelect)
    {
        Label = label;
        SelectCommand = new RelayCommand(onSelect);
    }

    public string Label { get; }
    public IRelayCommand SelectCommand { get; }
    [ObservableProperty] private bool _isSelected;
}

/// <summary>A pace button: the number plus its BPM band.</summary>
public sealed partial class PaceOption : ObservableObject
{
    public PaceOption(int number, string bpm, Action onSelect)
    {
        Number = number;
        Bpm = bpm;
        SelectCommand = new RelayCommand(onSelect);
    }

    public int Number { get; }
    public string Bpm { get; }
    public IRelayCommand SelectCommand { get; }
    [ObservableProperty] private bool _isSelected;
}
