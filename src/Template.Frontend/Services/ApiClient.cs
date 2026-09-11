namespace Template.Frontend.Services;

using System.Net.Http.Json;

// Calls the authenticated HTTP API.
//
// The HttpClient is wired to an AuthorizationMessageHandler that attaches the access token as a
// bearer header, but only for URLs under the API endpoint, so the token is never sent to S3 or
// Cognito. API Gateway verifies that token and rejects anything unauthenticated with 401 before
// the Lambda runs.
public sealed class ApiClient
{
    private readonly HttpClient client;

    public ApiClient(IHttpClientFactory factory)
    {
        client = factory.CreateClient(Name);
    }

    // Name of the configured client; shared with Program.cs so the registration stays in one place.
    public const string Name = "Api";

    public async Task<HelloResponse?> GetHelloAsync() =>
        await client.GetFromJsonAsync("hello", ApiSerializerContext.Default.HelloResponse);

    public async Task<EchoResponse?> PostEchoAsync(string message)
    {
        using var response = await client.PostAsJsonAsync(
            "echo", new EchoRequest(message), ApiSerializerContext.Default.EchoRequest);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(ApiSerializerContext.Default.EchoResponse);
    }
}
