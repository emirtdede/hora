using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Hora.UI.ViewModels;

namespace Hora.UI.Controls;

public partial class DonutChart : UserControl
{
    public DonutChart()
    {
        InitializeComponent();
    }

    public void Render(IReadOnlyList<UsageItemVm> items, long totalSeconds)
    {
        Ring.Children.Clear();
        CenterTotal.Text = UsageItemVm.FormatDuration(totalSeconds);
        if (items.Count == 0 || totalSeconds <= 0)
        {
            Ring.Children.Add(EmptyRing());
            return;
        }

        const double cx = 90, cy = 90, outer = 80, inner = 56;
        var angle = -90.0;
        var top = items.Take(7).ToList();
        foreach (var item in top)
        {
            var sweep = 360.0 * item.Seconds / totalSeconds;
            if (sweep <= 0)
            {
                continue;
            }

            Ring.Children.Add(Arc(cx, cy, outer, inner, angle, angle + sweep, item.Color));
            angle += sweep;
        }
    }

    private static Path Arc(double cx, double cy, double outer, double inner, double fromDeg, double toDeg, string colorHex)
    {
        var from = fromDeg * Math.PI / 180.0;
        var to = toDeg * Math.PI / 180.0;

        var x1 = cx + outer * Math.Cos(from);
        var y1 = cy + outer * Math.Sin(from);
        var x2 = cx + outer * Math.Cos(to);
        var y2 = cy + outer * Math.Sin(to);
        var x3 = cx + inner * Math.Cos(to);
        var y3 = cy + inner * Math.Sin(to);
        var x4 = cx + inner * Math.Cos(from);
        var y4 = cy + inner * Math.Sin(from);

        var fig = new PathFigure { StartPoint = new Point(x1, y1), IsClosed = true };
        var outerArc = new ArcSegment(new Point(x2, y2), new Size(outer, outer), 0, false, SweepDirection.Clockwise, true);
        outerArc.IsLargeArc = (toDeg - fromDeg) > 180;
        fig.Segments.Add(outerArc);
        fig.Segments.Add(new LineSegment(new Point(x3, y3), true));
        fig.Segments.Add(new ArcSegment(new Point(x4, y4), new Size(inner, inner), 0, false, SweepDirection.Counterclockwise, true));
        var geo = new PathGeometry(new[] { fig });
        var brush = (Brush)new BrushConverter().ConvertFromString(colorHex)!;
        return new Path { Data = geo, Fill = brush };
    }

    private static Path EmptyRing()
    {
        var geo = new EllipseGeometry(new Point(90, 90), 80, 80);
        var inner = new EllipseGeometry(new Point(90, 90), 56, 56);
        var combined = new CombinedGeometry(GeometryCombineMode.Exclude, geo, inner);
        return new Path { Data = combined, Fill = new SolidColorBrush(Color.FromRgb(0x33, 0x37, 0x4A)) };
    }
}
