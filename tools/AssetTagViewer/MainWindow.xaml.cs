using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AssetTagViewer;

public partial class MainWindow : Window
{
    // Case-insensitive read; write with camelCase names (matching the hand-authored keys) and the
    // relaxed encoder so characters like apostrophes/parentheses in notes stay human-readable.
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private string? _jsonPath;
    private AssetContent? _content;
    private bool _dirty;

    public MainWindow()
    {
        InitializeComponent();
        PaceBox.ItemsSource = new[] { 1, 2, 3, 4, 5 };

        string? path = ResolveStartupPath();
        if (path is not null && File.Exists(path))
            Load(path);
        else
            StatusText.Text = "No asset-content.json found — use Open… to pick one.";
    }

    /// <summary>CLI arg wins; otherwise default to the VerificationTease file under the repo root.</summary>
    private static string? ResolveStartupPath()
    {
        string[] args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
        {
            string p = args[1];
            if (Directory.Exists(p)) return Path.Combine(p, "asset-content.json");
            return p;
        }

        string? repoRoot = FindRepoRoot();
        return repoRoot is null
            ? null
            : Path.Combine(repoRoot, "milovana", "Teases", "VerificationTease", "asset-content.json");
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HismithController.slnx")))
            dir = dir.Parent;
        return dir?.FullName;
    }

    private void Load(string path)
    {
        try
        {
            string text = File.ReadAllText(path);
            AssetContent? content = JsonSerializer.Deserialize<AssetContent>(text, ReadOptions);
            if (content is null) throw new InvalidDataException("File did not parse to an AssetContent object.");

            _jsonPath = path;
            _content = content;

            // Drive the combo option lists from the file's own `vocabulary` block when present, so the
            // editor offers exactly the documented values (and stays in sync if the vocabulary changes).
            SubjectBox.ItemsSource = VocabArray("subject", "solo", "machine", "partner");
            ExplicitBox.ItemsSource = VocabArray("explicitness", "clothed", "underwear", "partial-nudity", "nude", "explicit");
            OrientationBox.ItemsSource = VocabArray("orientation", "portrait", "landscape");

            Tree.ItemsSource = BuildTree();
            _dirty = false;
            UpdateStatus();

            // Land on the first gallery so the theme + thumbnails are visible immediately.
            if (Tree.Items.Count > 0 && Tree.Items[0] is GalleryNode first)
            {
                first.IsSelected = true;
                ShowGallery(first);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load:\n{path}\n\n{ex.Message}", "Load error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string[] VocabArray(string key, params string[] fallback)
    {
        if (_content?.Vocabulary is JsonObject vo &&
            vo.TryGetPropertyValue(key, out JsonNode? node) && node is JsonArray arr)
        {
            var values = arr.Where(n => n is not null).Select(n => n!.GetValue<string>()).ToArray();
            if (values.Length > 0) return values;
        }
        return fallback;
    }

    private List<GalleryNode> BuildTree()
    {
        var nodes = new List<GalleryNode>();
        if (_content is null || _jsonPath is null) return nodes;

        string teaseDir = Path.GetDirectoryName(_jsonPath)!;
        foreach ((string galleryName, GalleryTags gallery) in _content.Galleries)
        {
            var images = new List<ImageItem>();
            foreach ((string fileName, ImageTags tags) in gallery.Images)
            {
                string? imagePath = ResolveImagePath(teaseDir, galleryName, fileName);
                var item = new ImageItem(galleryName, fileName, imagePath, tags);
                item.Edited += MarkDirty;
                images.Add(item);
            }
            var node = new GalleryNode(galleryName, gallery, images);
            node.Edited += MarkDirty;
            nodes.Add(node);
        }
        return nodes;
    }

    /// <summary>
    /// Resolve a local image: first the documented layout (<c>Gallery/&lt;name&gt;/&lt;file&gt;</c>),
    /// then a recursive fallback by filename so the tool still works if the folder layout differs.
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

    private void OnTreeSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        switch (e.NewValue)
        {
            case GalleryNode gallery:
                ShowGallery(gallery);
                break;
            case ImageItem image:
                ShowImage(image);
                break;
        }
    }

    private void ShowGallery(GalleryNode gallery)
    {
        EmptyHint.Visibility = Visibility.Collapsed;
        ImageView.Visibility = Visibility.Collapsed;
        GalleryView.Visibility = Visibility.Visible;

        GalleryView.DataContext = gallery;
        GalleryNameText.Text = gallery.Name;
        Thumbs.SelectedItem = null; // so clicking a thumbnail (even the same one) re-fires selection
    }

    private void ShowImage(ImageItem image)
    {
        EmptyHint.Visibility = Visibility.Collapsed;
        GalleryView.Visibility = Visibility.Collapsed;
        ImageView.Visibility = Visibility.Visible;

        ImageView.DataContext = image;
        Preview.Source = ImageLoader.Load(image.ImagePath);
        NoImageText.Visibility = Preview.Source is null ? Visibility.Visible : Visibility.Collapsed;
        WhichText.Text = image.Display + (image.ImagePath is null ? "   ⚠ image file not found" : string.Empty);
    }

    // Clicking a thumbnail selects that image in the tree, which switches to the image (tags) view.
    private void OnThumbSelected(object sender, SelectionChangedEventArgs e)
    {
        if (Thumbs.SelectedItem is ImageItem image)
            image.IsSelected = true;
    }

    private void MarkDirty()
    {
        _dirty = true;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        StatusText.Text = _jsonPath is null
            ? string.Empty
            : $"{_jsonPath}{(_dirty ? "   • unsaved changes" : string.Empty)}";
        Title = $"Asset Tag Viewer{(_dirty ? " *" : string.Empty)}";
    }

    private bool Save()
    {
        if (_content is null || _jsonPath is null) return false;
        try
        {
            // Commit any pending edit still focused in an editable combo (LostFocus hasn't fired yet).
            SubjectBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
            ExplicitBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
            OrientationBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();

            File.WriteAllText(_jsonPath, JsonSerializer.Serialize(_content, WriteOptions) + "\n");
            _dirty = false;
            UpdateStatus();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save:\n{_jsonPath}\n\n{ex.Message}", "Save error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardIfDirty()) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Asset content (asset-content.json)|asset-content.json|JSON files (*.json)|*.json",
            Title = "Open asset-content.json",
        };
        if (dlg.ShowDialog() == true) Load(dlg.FileName);
    }

    private void OnSave(object sender, RoutedEventArgs e) => Save();

    private void OnSaveCommand(object sender, ExecutedRoutedEventArgs e) => Save();

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmDiscardIfDirty()) e.Cancel = true;
    }

    /// <summary>Returns false if the user cancels (caller should abort the navigation/close).</summary>
    private bool ConfirmDiscardIfDirty()
    {
        if (!_dirty) return true;
        MessageBoxResult r = MessageBox.Show("Save changes first?", "Unsaved changes",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return r switch
        {
            MessageBoxResult.Yes => Save(),
            MessageBoxResult.No => true,
            _ => false,
        };
    }
}
