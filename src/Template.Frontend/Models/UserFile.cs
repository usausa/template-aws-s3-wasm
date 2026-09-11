namespace Template.Frontend.Models;

// File entry for the list view. Name is the path relative to users/{sub}/.
public sealed record UserFile(string Key, string Name, long Size, DateTime? LastModified);
