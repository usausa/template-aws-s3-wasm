namespace Frontend;

internal static partial class Log
{
    // Auth

    [LoggerMessage(Level = LogLevel.Information, Message = "AWS credentials acquired. identityId=[{identityId}], expiration=[{expiration}]")]
    public static partial void InfoCredentialsAcquired(this ILogger log, string identityId, DateTime expiration);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token unavailable. reason=[{reason}]")]
    public static partial void WarnTokenUnavailable(this ILogger log, string reason);

    // Files

    [LoggerMessage(Level = LogLevel.Information, Message = "User files listed. count=[{count}], prefix=[{prefix}], elapsedMs=[{elapsedMs}]")]
    public static partial void InfoFilesListed(this ILogger log, int count, string prefix, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "File operation failed. operation=[{operation}]")]
    public static partial void ErrorFileOperation(this ILogger log, string operation, Exception ex);
}
