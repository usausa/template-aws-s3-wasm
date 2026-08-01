namespace Frontend.Application;

using Amazon.Runtime;

// Adapts the AWS SDK HTTP stack to browser-wasm. Registered via AWSConfigs.HttpClientFactory.
//
// Two browser constraints have to be met:
//   - The SDK default factory configures SocketsHttpHandler, which throws
//     PlatformNotSupportedException in the browser (all traffic must go through fetch).
//     Building the client from HttpClientHandler lets the runtime pick the browser handler.
//   - The SDK unmarshallers read the response body synchronously, but the browser response
//     stream only supports async reads (net_http_synchronous_reads_not_supported).
//     Buffering each response up front turns it into a MemoryStream, which reads either way.
internal sealed class BrowserHttpClientFactory : HttpClientFactory
{
    // One handler chain for the whole app; clients borrow it instead of building their own.
    private static readonly BufferingHandler SharedHandler = new();

    public override HttpClient CreateHttpClient(IClientConfig clientConfig) =>
        new(SharedHandler, disposeHandler: false);

    private sealed class BufferingHandler : DelegatingHandler
    {
        // HttpClientHandler resolves to the browser handler here. The base class owns it
        // from this point and disposes it along with this handler.
        public BufferingHandler()
        {
            InnerHandler = new HttpClientHandler();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            return response;
        }
    }
}
