namespace Frontend.Models;

// One measurement row of a data file.
public sealed record SeriesPoint(DateOnly Date, double Value, string Note);
