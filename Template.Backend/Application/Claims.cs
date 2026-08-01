namespace Template.Backend.Application;

// Reads the claims the API Gateway authorizer already verified. Stateless, so it stays a static
// helper rather than being pushed through the container.
public static class Claims
{
    public static string Sub(APIGatewayHttpApiV2ProxyRequest request) => Read(request, "sub");

    public static string Username(APIGatewayHttpApiV2ProxyRequest request) => Read(request, "username");

    private static string Read(APIGatewayHttpApiV2ProxyRequest request, string name)
    {
        var claims = request.RequestContext?.Authorizer?.Jwt?.Claims;
        return (claims is not null) && claims.TryGetValue(name, out var value) ? value : string.Empty;
    }
}
