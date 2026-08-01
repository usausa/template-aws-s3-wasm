namespace Frontend.Components.Chart;

using System.Text;

using Frontend.Models;

public sealed partial class SeriesChart
{
    private const double Width = 720;
    private const double Height = 260;
    private const double PadLeft = 52;
    private const double PadRight = 12;
    private const double PadTop = 12;
    private const double PadBottom = 26;

    private const int GridLineCount = 4;

    [Parameter]
    [EditorRequired]
    public required DataSeries Series { get; set; }

    private static string ViewBox => string.Create(CultureInfo.InvariantCulture, $"0 0 {Width} {Height}");

    //--------------------------------------------------------------------------------
    // Scale
    //--------------------------------------------------------------------------------

    // The value axis starts at zero so bar-like magnitude comparisons stay honest,
    // with a 10% headroom above the peak so the line never touches the top edge.
    private double AxisMaximum => Series.Maximum > 0 ? Series.Maximum * 1.1 : 1;

    private double X(int index) =>
        Series.Points.Count <= 1
            ? PadLeft
            : PadLeft + ((Width - PadLeft - PadRight) * index / (Series.Points.Count - 1));

    private double Y(double value) =>
        Height - PadBottom - ((Height - PadTop - PadBottom) * value / AxisMaximum);

    //--------------------------------------------------------------------------------
    // Geometry
    //--------------------------------------------------------------------------------

    private string LinePoints() =>
        String.Join(' ', Series.Points.Select((point, index) =>
            string.Create(CultureInfo.InvariantCulture, $"{X(index):F1},{Y(point.Value):F1}")));

    // Closing the line down to the baseline gives the filled band under the curve.
    private string AreaPoints() =>
        string.Create(CultureInfo.InvariantCulture, $"{PadLeft:F1},{Height - PadBottom:F1} ") +
        LinePoints() +
        string.Create(CultureInfo.InvariantCulture, $" {X(Series.Points.Count - 1):F1},{Height - PadBottom:F1}");

    private IEnumerable<GridLine> GridLines()
    {
        for (var i = 0; i <= GridLineCount; i++)
        {
            var value = AxisMaximum * i / GridLineCount;
            yield return new GridLine(Y(value), value.ToString("F0", CultureInfo.InvariantCulture));
        }
    }

    //--------------------------------------------------------------------------------
    // Labels
    //--------------------------------------------------------------------------------

    // Every value here is produced from parsed numbers and dates, so the markup carries
    // no caller-supplied text and needs no escaping.
    private MarkupString Labels()
    {
        var builder = new StringBuilder();

        foreach (var line in GridLines())
        {
            Append(PadLeft - 6, line.Y + 4, "end", line.Label);
        }

        // First, middle and last date only. Labelling every point turns unreadable past
        // a couple of weeks of data.
        var last = Series.Points.Count - 1;
        Append(X(0), Height - 8, "start", DateLabel(0));

        if (last >= 2)
        {
            Append(X(last / 2), Height - 8, "middle", DateLabel(last / 2));
        }

        if (last >= 1)
        {
            Append(X(last), Height - 8, "end", DateLabel(last));
        }

        return new MarkupString(builder.ToString());

        void Append(double x, double y, string anchor, string label) =>
            builder.Append(CultureInfo.InvariantCulture, $"<text class=\"chart-label\" x=\"{F(x)}\" y=\"{F(y)}\" text-anchor=\"{anchor}\">{label}</text>");

        string DateLabel(int index) =>
            Series.Points[index].Date.ToString("MM-dd", CultureInfo.InvariantCulture);
    }

    private static string F(double value) => value.ToString("F1", CultureInfo.InvariantCulture);

    //--------------------------------------------------------------------------------
    // Types
    //--------------------------------------------------------------------------------

    private sealed record GridLine(double Y, string Label);
}
