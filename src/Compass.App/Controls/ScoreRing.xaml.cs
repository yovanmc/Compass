using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Compass.App.Controls;

public partial class ScoreRing : UserControl
{
    public static readonly DependencyProperty PercentProperty =
        DependencyProperty.Register(nameof(Percent), typeof(int), typeof(ScoreRing),
            new PropertyMetadata(0, OnPercentChanged));

    public int Percent
    {
        get => (int)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    private static void OnPercentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ScoreRing)d).UpdateArc();

    public ScoreRing()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateArc();
    }

    private void UpdateArc()
    {
        ScoreLabel.Text = $"{Percent}%";

        int pct = Math.Clamp(Percent, 0, 100);

        if (pct <= 0)
        {
            ArcPath.Data = null;
            return;
        }

        // Full circle: draw two 180-degree arcs so we can handle pct==100 (ArcSegment
        // treats start==end as a no-op if angle is 360).
        const double cx = 25;
        const double cy = 25;
        const double r = 22.5; // matches (50/2) - strokeThickness/2 = 25-2.5

        double angle = pct == 100 ? 359.999 : pct / 100.0 * 360.0;
        double radians = (angle - 90) * Math.PI / 180.0; // start from top (12 o'clock)

        double startX = cx + r * Math.Cos(-Math.PI / 2);  // top
        double startY = cy + r * Math.Sin(-Math.PI / 2);  // top
        double endX = cx + r * Math.Cos(radians - Math.PI / 2 + Math.PI / 2);
        double endY = cy + r * Math.Sin(radians - Math.PI / 2 + Math.PI / 2);

        // Recalculate cleanly
        double startAngleDeg = -90;
        double endAngleDeg = startAngleDeg + angle;
        double startRad = startAngleDeg * Math.PI / 180.0;
        double endRad = endAngleDeg * Math.PI / 180.0;

        double sx = cx + r * Math.Cos(startRad);
        double sy = cy + r * Math.Sin(startRad);
        double ex = cx + r * Math.Cos(endRad);
        double ey = cy + r * Math.Sin(endRad);

        bool isLargeArc = angle > 180;

        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(sx, sy) };
        fig.Segments.Add(new ArcSegment(
            new Point(ex, ey),
            new Size(r, r),
            0,
            isLargeArc,
            SweepDirection.Clockwise,
            true));
        geo.Figures.Add(fig);
        ArcPath.Data = geo;
    }
}
