namespace Template.Backend.Functions;

using Amazon.Lambda.Annotations;

using Template.Backend.Services;

// GET /hello - returns the identity the API Gateway authorizer verified.
//
// Only [LambdaFunction] is used, not [HttpApi]: routing stays in the CDK stack so the whole
// infrastructure remains described in one place. The annotation is here for the generated
// wrapper and dependency injection, not for deployment.
public sealed class HelloFunction
{
    private readonly InvocationCounter counter;

    public HelloFunction(InvocationCounter counter)
    {
        this.counter = counter;
    }

    [LambdaFunction]
    public APIGatewayHttpApiV2ProxyResponse Handle(
        APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var sub = Claims.Sub(request);
        var invocation = counter.Next();

        context.Logger.LogInformation(
            $"Hello requested. sub=[{sub}], invocation=[{invocation}], requestId=[{context.AwsRequestId}]");

        var payload = new HelloResponse(
            "Hello from Lambda",
            sub,
            Claims.Username(request),
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            invocation,
            context.AwsRequestId);

        return Json.Ok(payload, FunctionSerializerContext.Default.HelloResponse);
    }
}
