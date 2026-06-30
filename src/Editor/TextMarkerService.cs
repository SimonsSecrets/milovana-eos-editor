using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace MilovanaEosEditor.Editor;

/// <summary>A diagnostic squiggle: a document span, its colour (error red / warning amber), and the
/// tooltip message.</summary>
internal sealed record Squiggle(int StartOffset, int Length, Brush Color, string Message)
{
    public int EndOffset => StartOffset + Math.Max(1, Length);
}

/// <summary>
/// Draws wavy underlines for the current diagnostics under the AvalonEdit text (an
/// <see cref="IBackgroundRenderer"/>), and answers "which marker is at this offset?" for hover
/// tooltips. Markers are replaced wholesale after each compile.
/// </summary>
internal sealed class SquiggleRenderer : IBackgroundRenderer
{
    private readonly TextView _textView;
    private IReadOnlyList<Squiggle> _markers = Array.Empty<Squiggle>();

    public SquiggleRenderer(TextView textView) => _textView = textView;

    public KnownLayer Layer => KnownLayer.Selection;

    public void SetMarkers(IEnumerable<Squiggle> markers)
    {
        _markers = markers.ToList();
        _textView.InvalidateLayer(Layer);
    }

    public Squiggle? MarkerAt(int offset) =>
        _markers.FirstOrDefault(m => offset >= m.StartOffset && offset < m.EndOffset);

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_markers.Count == 0 || !textView.VisualLinesValid) return;
        var lines = textView.VisualLines;
        if (lines.Count == 0) return;

        int viewStart = lines[0].FirstDocumentLine.Offset;
        int viewEnd = lines[^1].LastDocumentLine.EndOffset;

        foreach (Squiggle marker in _markers)
        {
            if (marker.EndOffset < viewStart || marker.StartOffset > viewEnd) continue;
            var segment = new SimpleSegment(marker.StartOffset, Math.Max(1, marker.Length));
            foreach (Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                DrawWavy(drawingContext, r, marker.Color);
        }
    }

    private static void DrawWavy(DrawingContext dc, Rect r, Brush brush)
    {
        var pen = new Pen(brush, 1.0);
        pen.Freeze();
        double y = r.Bottom - 1;
        const double step = 2.0;

        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(r.Left, y), false, false);
            bool up = true;
            for (double x = r.Left + step; x < r.Right; x += step)
            {
                ctx.LineTo(new Point(x, up ? y - 2 : y), true, false);
                up = !up;
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private sealed class SimpleSegment : ISegment
    {
        public SimpleSegment(int offset, int length) { Offset = offset; Length = length; }
        public int Offset { get; }
        public int Length { get; }
        public int EndOffset => Offset + Length;
    }
}
