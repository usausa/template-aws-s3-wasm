namespace Template.Backend.Models;

public sealed record HelloResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("sub")] string Sub,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("invokedAt")] string InvokedAt,
    [property: JsonPropertyName("invocation")] int Invocation,
    [property: JsonPropertyName("requestId")] string RequestId);
