namespace Backend;

// Dummy API behind an API Gateway HTTP API with a Cognito JWT authorizer.
//
// The authorizer verifies the bearer token (signature, issuer, client id) before the request
// reaches here, so an unauthenticated or tampered call is rejected with 401 and this code never
// runs. That is what makes the claims below trustworthy: they come from the authorizer, not from
// anything the caller can set. Real handlers should scope their work by the 'sub' claim the same
// way the S3 policy does.
public sealed class Function
{
    public static APIGatewayHttpApiV2ProxyResponse Handle(
        APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var claims = request.RequestContext?.Authorizer?.Jwt?.Claims;
        var sub = Claim(claims, "sub");

        context.Logger.LogInformation(
            $"Hello requested. sub=[{sub}], requestId=[{context.AwsRequestId}]");

        var payload = new HelloResponse(
            "Hello from Lambda",
            sub,
            Claim(claims, "username"),
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            context.AwsRequestId);

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Content-Type"] = "application/json",
            },
            Body = JsonSerializer.Serialize(payload, FunctionSerializerContext.Default.HelloResponse),
        };
    }

    private static string Claim(IDictionary<string, string>? claims, string name) =>
        (claims is not null) && claims.TryGetValue(name, out var value) ? value : string.Empty;
}

public sealed record HelloResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("sub")] string Sub,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("invokedAt")] string InvokedAt,
    [property: JsonPropertyName("requestId")] string RequestId);

[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
[JsonSerializable(typeof(HelloResponse))]
public sealed partial class FunctionSerializerContext : JsonSerializerContext;
