namespace Frontend.Models;

using System.Text.Json.Serialization;

// Payload returned by the Lambda behind the authenticated API. The claim values are the ones
// the API Gateway authorizer verified, so they show which identity the backend actually saw.
public sealed record HelloResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("sub")] string Sub,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("invokedAt")] string InvokedAt,
    [property: JsonPropertyName("requestId")] string RequestId);

[JsonSerializable(typeof(HelloResponse))]
public sealed partial class ApiSerializerContext : JsonSerializerContext;
