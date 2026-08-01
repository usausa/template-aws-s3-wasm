namespace IaC;

using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AwsApigatewayv2Authorizers;
using Amazon.CDK.AwsApigatewayv2Integrations;

// Both the API and Lambda namespaces define HttpMethod.
using HttpMethod = Amazon.CDK.AWS.Apigatewayv2.HttpMethod;

// Authenticated API: HTTP API -> Cognito JWT authorizer -> Lambda.
//
// The authorizer is attached as the API default, so every route requires a valid bearer token
// and an unauthenticated call is rejected with 401 before the function is invoked. That is the
// reason the handler carries no authorization logic of its own: by the time a request reaches
// it, the token is verified and its claims can be trusted.
//
// Authorization here is token-based, unlike the S3 path which uses IAM with temporary
// credentials. Both start from the same Cognito sign-in; they differ in what evaluates the
// permission (API Gateway verifying a JWT vs. S3 evaluating an IAM policy).
public sealed class ApiConstruct : Construct
{
    public ApiConstruct(
        Construct scope,
        string id,
        EnvironmentConfig config,
        string appOrigin,
        IUserPool userPool,
        IUserPoolClient userPoolClient)
        : base(scope, id)
    {
        // Published output of the Backend project. Built by scripts/deploy-api.ps1 before
        // synthesis; CDK zips the directory as an asset.
        var artifact = System.IO.Path.Combine(
            System.IO.Directory.GetCurrentDirectory(), "..", "publish-api");

        Function = new Function(this, "Function", new FunctionProps
        {
            Runtime = Runtime.DOTNET_10,
            Handler = "Backend::Backend.Function::Handle",
            Code = Code.FromAsset(artifact),
            MemorySize = 256,
            Timeout = Duration.Seconds(10),
            LogGroup = new LogGroup(this, "Logs", new LogGroupProps
            {
                Retention = config.Ephemeral ? RetentionDays.ONE_WEEK : RetentionDays.ONE_MONTH,
                RemovalPolicy = config.Ephemeral ? RemovalPolicy.DESTROY : RemovalPolicy.RETAIN,
            }),
            Description = $"Authenticated dummy API ({config.EnvName})",
        });

        var origins = new List<string> { appOrigin };
        if (config.AllowLocalhost)
        {
            origins.Add(EnvironmentConfig.LocalhostOrigin);
        }

        var authorizer = new HttpUserPoolAuthorizer("Authorizer", userPool, new HttpUserPoolAuthorizerProps
        {
            UserPoolClients = [userPoolClient],

            // The app calls the API with an access token, whose audience lives in the
            // 'client_id' claim rather than 'aud'.
            IdentitySource = ["$request.header.Authorization"],
        });

        Api = new HttpApi(this, "Api", new HttpApiProps
        {
            // Applied to every route, so no route can be added without authentication.
            DefaultAuthorizer = authorizer,
            CorsPreflight = new CorsPreflightOptions
            {
                AllowOrigins = [.. origins],
                AllowMethods = [CorsHttpMethod.GET],
                AllowHeaders = ["authorization", "content-type"],
                MaxAge = Duration.Hours(1),
            },
            Description = $"User file portal API ({config.EnvName})",
        });

        Api.AddRoutes(new AddRoutesOptions
        {
            Path = "/hello",
            Methods = [HttpMethod.GET],
            Integration = new HttpLambdaIntegration("HelloIntegration", Function),
        });
    }

    public Function Function { get; }

    public HttpApi Api { get; }
}
