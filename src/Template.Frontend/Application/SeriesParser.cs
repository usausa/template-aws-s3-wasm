namespace Template.Frontend.Application;

using Template.Frontend.Models;

// Parses the "date,value,note" CSV layout used by the sample data.
//
// The whole file is small enough to parse in the browser on demand, which is what makes
// the API-less design work: S3 hands over the raw object and the client does the rest.
// A dataset large enough to need server-side aggregation would need an API layer instead.
public static class SeriesParser
{
    private const string Header = "date,value,note";

    private static readonly char[] LineSeparators = ['\n'];

    // Returns null when the content is not in the expected layout, so the caller can
    // fall back to a plain text view instead of showing an error.
    public static DataSeries? Parse(string name, string content)
    {
        // Line endings vary with whatever wrote the object, so split on \n and trim \r.
        var lines = content
            .Split(LineSeparators, StringSplitOptions.None)
            .Select(static x => x.TrimEnd('\r'))
            .ToArray();

        if ((lines.Length < 2) || !String.Equals(lines[0].Trim(), Header, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var points = new List<SeriesPoint>(lines.Length - 1);
        for (var i = 1; i < lines.Length; i++)
        {
            if (String.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var cells = lines[i].Split(',');
            if (cells.Length < 2)
            {
                return null;
            }

            if (!DateOnly.TryParseExact(cells[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                !Double.TryParse(cells[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            points.Add(new SeriesPoint(date, value, cells.Length > 2 ? cells[2] : string.Empty));
        }

        return points.Count > 0 ? new DataSeries(name, points) : null;
    }
}
