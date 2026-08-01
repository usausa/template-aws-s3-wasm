namespace Backend.Models;

public sealed record EchoRequest(
    [property: JsonPropertyName("message")] string? Message);

public sealed record EchoResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("length")] int Length,
    [property: JsonPropertyName("sub")] string Sub,
    [property: JsonPropertyName("receivedAt")] string ReceivedAt,
    [property: JsonPropertyName("requestId")] string RequestId);

public sealed record ErrorResponse(
    [property: JsonPropertyName("error")] string Error);
