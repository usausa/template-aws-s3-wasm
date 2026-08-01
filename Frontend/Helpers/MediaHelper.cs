namespace Frontend.Helpers;

// Media type helpers. Only text preview is supported by this template;
// extend the sets here when adding image/video preview.
public static class MediaHelper
{
    private static readonly HashSet<string> PreviewTextExtensions =
        new([".txt", ".json", ".csv", ".md", ".log", ".xml", ".yaml", ".yml"], StringComparer.OrdinalIgnoreCase);

    public static bool IsPreviewableText(string path) =>
        PreviewTextExtensions.Contains(Path.GetExtension(path));
}
