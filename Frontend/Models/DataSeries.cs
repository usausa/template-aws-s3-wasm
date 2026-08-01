namespace Frontend.Models;

// A parsed data file together with the summary values shown above the chart.
// The summary is computed once here rather than in the markup, because the page
// re-renders on every interaction while the series itself does not change.
public sealed class DataSeries
{
    public DataSeries(string name, IReadOnlyList<SeriesPoint> points)
    {
        Name = name;
        Points = points;

        PeakIndex = -1;

        for (var i = 0; i < points.Count; i++)
        {
            if ((PeakIndex < 0) || (points[i].Value > points[PeakIndex].Value))
            {
                PeakIndex = i;
            }
        }

        if (points.Count > 0)
        {
            Minimum = points.Min(static x => x.Value);
            Maximum = points.Max(static x => x.Value);
            Average = points.Average(static x => x.Value);
            Total = points.Sum(static x => x.Value);
            Peak = points[PeakIndex];
        }
    }

    public string Name { get; }

    public IReadOnlyList<SeriesPoint> Points { get; }

    public double Minimum { get; }

    public double Maximum { get; }

    public double Average { get; }

    public double Total { get; }

    public SeriesPoint? Peak { get; }

    // Index of the peak point, or -1 for an empty series. The chart needs the position,
    // not just the value, to place its marker.
    public int PeakIndex { get; }
}
