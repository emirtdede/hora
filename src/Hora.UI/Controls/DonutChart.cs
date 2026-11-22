using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Hora.UI.Controls;

/// <summary>Donut chart drawn with geometry so it stays crisp at any size or scale.</summary>
public sealed class DonutChart : FrameworkElement
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(IReadOnlyList<DonutSegment>), typeof(DonutChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(DonutChart),
        new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CenterTextProperty = DependencyProperty.Register(
        nameof(CenterText), typeof(string), typeof(DonutChart),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CaptionTextProperty = DependencyProperty.Register(
        nameof(CaptionText), typeof(string), typeof(DonutChart),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<DonutSegment>? Segments
    {
        get => (IReadOnlyList<DonutSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public string CenterText
    {
        get => (string)GetValue(CenterTextProperty);
        set => SetValue(CenterTextProperty, value);
    }

    public string CaptionText
    {
        get => (string)GetValue(CaptionTextProperty);
        set => SetValue(CaptionTextProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 8)
        {
            return;
        }

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = (size / 2) - (Thickness / 2) - 1;
        var trackBrush = TryBrush("BrushSurfaceAlt", Color.FromRgb(0x1A, 0x20, 0x2D));
        var trackPen = new Pen(trackBrush, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        drawingContext.DrawEllipse(null, trackPen, center, radius, radius);

        var segments = Segments;
        if (segments is null || segments.Count == 0)
        {
            DrawLabels(drawingContext, center);
            return;
        }

        const double gap = 2.4;
        var startAngle = -90.0;
        var total = segments.Sum(s => Math.Max(0, s.Value));
        if (total <= 0)
        {
            DrawLabels(drawingContext, center);
            return;
        }

        foreach (var segment in segments)
        {
            var sweep = segment.Value / total * 360.0;
            if (sweep <= 0.2)
            {
                continue;
            }

            var usable = Math.Max(0.6, sweep - gap);
            var geometry = ArcGeometry(center, radius, startAngle + (gap / 2), usable);
            var pen = new Pen(new SolidColorBrush(segment.Color), Thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };
            pen.Freeze();
            drawingContext.DrawGeometry(null, pen, geometry);
            startAngle += sweep;
        }

        DrawLabels(drawingContext, center);
    }

    private void DrawLabels(DrawingContext context, Point center)
    {
        var typeface = new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"),
            FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        if (!string.IsNullOrWhiteSpace(CenterText))
        {
            var formatted = new FormattedText(CenterText, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, Math.Max(16, ActualWidth * 0.11),
                TryBrush("BrushForeground", Colors.White), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            context.DrawText(formatted,
                new Point(center.X - (formatted.Width / 2), center.Y - formatted.Height + 2));
        }

        if (!string.IsNullOrWhiteSpace(CaptionText))
        {
            var captionTypeface = new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"),
                FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var caption = new FormattedText(CaptionText, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, captionTypeface, Math.Max(9, ActualWidth * 0.045),
                TryBrush("BrushMuted", Color.FromRgb(0x98, 0xA2, 0xB8)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            context.DrawText(caption, new Point(center.X - (caption.Width / 2), center.Y + 4));
        }
    }

    private static StreamGeometry ArcGeometry(Point center, double radius, double startDegrees, double sweepDegrees)
    {
        var start = PointOnCircle(center, radius, startDegrees);
        var end = PointOnCircle(center, radius, startDegrees + sweepDegrees);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.ArcTo(end, new Size(radius, radius), 0, sweepDegrees > 180,
                SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180.0;
        return new Point(center.X + (radius * Math.Cos(radians)), center.Y + (radius * Math.Sin(radians)));
    }

    private static Brush TryBrush(string key, Color fallback)
    {
        var brush = Application.Current?.TryFindResource(key) as Brush;
        return brush ?? new SolidColorBrush(fallback);
    }
}

/// <summary>One chart segment.</summary>
public sealed record DonutSegment(string Label, double Value, Color Color);
