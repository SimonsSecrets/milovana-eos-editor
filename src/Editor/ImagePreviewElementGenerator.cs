using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace MilovanaEosEditor.Editor;

/// <summary>
/// Injects a collapsible <c>▸ preview</c> chip at the end of every <c>[IMAGE: bucket/file.jpg]</c> line;
/// expanding it shows the resolved thumbnail inline (the line grows to fit, so following lines reflow —
/// the closest AvalonEdit equivalent of the mockup's expandable preview row). Clicking the chip toggles
/// the preview; clicking the thumbnail opens the picker to swap the image. The injected element consumes
/// zero document characters, so the marker text stays fully editable.
/// </summary>
internal sealed partial class ImagePreviewElementGenerator : VisualLineElementGenerator
{
    private readonly TextView _textView;
    private readonly Action<int, string> _openPicker;
    private readonly HashSet<int> _expandedLines = new();
    private readonly HashSet<int> _injectedOffsets = new();

    public ImagePreviewElementGenerator(TextView textView, AssetCatalog assets, Action<int, string> openPicker)
    {
        _textView = textView;
        Assets = assets;
        _openPicker = openPicker;
    }

    public AssetCatalog Assets { get; set; }

    [GeneratedRegex(@"^\[IMAGE:\s*(.+?)\]", RegexOptions.IgnoreCase)]
    private static partial Regex ImageRegex();

    public override void StartGeneration(ITextRunConstructionContext context)
    {
        base.StartGeneration(context);
        _injectedOffsets.Clear();
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        TextDocument doc = _textView.Document;
        DocumentLine line = doc.GetLineByOffset(startOffset);
        while (line != null)
        {
            if (line.EndOffset >= startOffset && !_injectedOffsets.Contains(line.EndOffset) && PayloadOf(line) is not null)
                return line.EndOffset;
            line = line.NextLine;
        }
        return -1;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        TextDocument doc = _textView.Document;
        DocumentLine line = doc.GetLineByOffset(offset);
        if (offset != line.EndOffset) return null;

        string? bucketFile = PayloadOf(line);
        if (bucketFile is null) return null;

        _injectedOffsets.Add(offset);
        UIElement chip = BuildPreview(bucketFile, line.LineNumber);
        return new InlineObjectElement(0, chip);
    }

    /// <summary>The image reference of an IMAGE line, or null if the line isn't a previewable image
    /// (prose, hold, or empty).</summary>
    private string? PayloadOf(DocumentLine line)
    {
        if (line.Length == 0) return null;
        string text = _textView.Document.GetText(line.Offset, line.Length);
        Match m = ImageRegex().Match(text);
        if (!m.Success) return null;
        string payload = m.Groups[1].Value.Trim();
        return payload.Length == 0 || payload.Equals("hold", StringComparison.OrdinalIgnoreCase) ? null : payload;
    }

    private UIElement BuildPreview(string bucketFile, int lineNumber)
    {
        bool expanded = _expandedLines.Contains(lineNumber);
        var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(12, 0, 0, 0) };
        panel.Children.Add(BuildChip(bucketFile, lineNumber, expanded));
        if (expanded) panel.Children.Add(BuildThumbnail(bucketFile, lineNumber));
        return panel;
    }

    private FrameworkElement BuildChip(string bucketFile, int lineNumber, bool expanded)
    {
        var content = new TextBlock
        {
            Text = (expanded ? "▾ " : "▸ ") + "preview",
            FontSize = 9.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
        };
        var chip = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xfa, 0xfa, 0xfa)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(6, 1, 7, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Child = content,
            ToolTip = bucketFile,
        };
        chip.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (!_expandedLines.Add(lineNumber)) _expandedLines.Remove(lineNumber);
            _textView.Redraw();
        };
        return chip;
    }

    private FrameworkElement BuildThumbnail(string bucketFile, int lineNumber)
    {
        string? path = Assets.ThumbnailPath(bucketFile);
        bool resolved = Assets.ResolveImageLocator(bucketFile) is not null;
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xe4, 0xe4, 0xe4)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(0xfa, 0xfa, 0xfa)),
            Margin = new Thickness(0, 4, 0, 6),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            ToolTip = resolved ? "Click to pick or swap image" : "No locator yet — click to pick an image",
        };

        BitmapImage? bmp = ImageLoader.Load(path, decodePixelWidth: 240);
        if (bmp is not null)
        {
            border.Child = new Image { Source = bmp, Width = 120, Height = 78, Stretch = Stretch.UniformToFill };
            border.ClipToBounds = true;
        }
        else
        {
            border.Child = new TextBlock
            {
                Text = path is null ? "not on disk" : "no preview",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0xaa, 0xaa, 0xaa)),
                Width = 120,
                Height = 78,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 32, 0, 0),
            };
        }

        border.MouseLeftButtonUp += (_, e) => { e.Handled = true; _openPicker(lineNumber, bucketFile); };
        return border;
    }
}
