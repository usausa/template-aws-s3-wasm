namespace Template.Frontend.Models;

public sealed record UserFilePage(IReadOnlyList<UserFile> Files, string? ContinuationToken);
