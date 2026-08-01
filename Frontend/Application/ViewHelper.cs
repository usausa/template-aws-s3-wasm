namespace Frontend.Application;

// Static helpers used directly from markup via '@using static Frontend.Application.ViewHelper'.
public static class ViewHelper
{
    public static string FormatBytes(long size) =>
        size switch
        {
            >= 1_073_741_824 => ((double)size / 1_073_741_824).ToString("F2", CultureInfo.InvariantCulture) + " GB",
            >= 1_048_576 => ((double)size / 1_048_576).ToString("F1", CultureInfo.InvariantCulture) + " MB",
            >= 1024 => ((double)size / 1024).ToString("F1", CultureInfo.InvariantCulture) + " KB",
            _ => size.ToString(CultureInfo.InvariantCulture) + " B",
        };

    public static string FormatStamp(DateTime? stamp) =>
        stamp.HasValue
            ? stamp.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "-";
}
