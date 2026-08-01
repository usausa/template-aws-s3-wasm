namespace Backend.Functions;

using Amazon.Lambda.Annotations;

// POST /echo - accepts a JSON body and returns it alongside the verified identity.
//
// Second endpoint of the template, deliberately different from /hello: it takes a request body
// and uses a different HTTP method, which also exercises the CORS preflight path.
public sealed class EchoFunction
{
    private const int MessageLimit = 200;

    // The generated wrapper instantiates this class and calls the method on it, so the handler
    // cannot be static even when it needs no injected state.
#pragma warning disable CA1822
    [LambdaFunction]
    public APIGatewayHttpApiV2ProxyResponse Handle(
        APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var sub = Claims.Sub(request);

        EchoRequest? body;
        try
        {
            body = String.IsNullOrEmpty(request.Body)
                ? null
                : JsonSerializer.Deserialize(request.Body, FunctionSerializerContext.Default.EchoRequest);
        }
        catch (JsonException)
        {
            return Json.BadRequest("Request body is not valid JSON.");
        }

        if (String.IsNullOrWhiteSpace(body?.Message))
        {
            return Json.BadRequest("A non-empty 'message' is required.");
        }

        // Trim rather than reject: the value is echoed straight back, so bounding it keeps the
        // response predictable without failing an otherwise fine request.
        var message = body.Message.Length > MessageLimit
            ? body.Message[..MessageLimit]
            : body.Message;

        context.Logger.LogInformation(
            $"Echo requested. sub=[{sub}], length=[{message.Length}], requestId=[{context.AwsRequestId}]");

        var payload = new EchoResponse(
            message,
            message.Length,
            sub,
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            context.AwsRequestId);

        return Json.Ok(payload, FunctionSerializerContext.Default.EchoResponse);
    }
#pragma warning restore CA1822
}
