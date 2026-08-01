namespace Template.Backend.Application;

using System.Text.Json.Serialization.Metadata;

// Builds the API Gateway responses. Kept in one place so every function returns the same shape
// and headers; CORS headers are not set here because the HTTP API handles them.
public static class Json
{
    public static APIGatewayHttpApiV2ProxyResponse Ok<T>(T payload, JsonTypeInfo<T> typeInfo) =>
        Build(200, JsonSerializer.Serialize(payload, typeInfo));

    public static APIGatewayHttpApiV2ProxyResponse BadRequest(string message) =>
        Build(400, JsonSerializer.Serialize(new ErrorResponse(message), FunctionSerializerContext.Default.ErrorResponse));

    private static APIGatewayHttpApiV2ProxyResponse Build(int statusCode, string body) =>
        new()
        {
            StatusCode = statusCode,
            Headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Content-Type"] = "application/json",
            },
            Body = body,
        };
}
