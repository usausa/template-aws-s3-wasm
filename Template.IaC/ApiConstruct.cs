namespace Template.IaC;

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
// and an unauthenticated call is rejected with 401 before any function is invoked. That is the
// reason the handlers carry no authorization logic of their own: by the time a request reaches
// them, the token is verified and its claims can be trusted.
//
// Authorization here is token-based, unlike the S3 path which uses IAM with temporary
// credentials. Both start from the same Cognito sign-in; they differ in what evaluates the
// permission (API Gateway verifying a JWT vs. S3 evaluating an IAM policy).
//
// Routing lives here rather than in [HttpApi] annotations so that the whole infrastructure stays
// described in one place. The handler names below are produced by the Lambda Annotations source
// generator: {Assembly}::{Namespace}.{Class}_{Method}_Generated::{Method}.
public sealed class ApiConstruct : Construct
{
    // Published output of the Template.Backend project, produced by scripts/deploy-api.ps1. Every function
    // shares this one artifact and differs only by handler.
    private static readonly string Artifact =
        System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "publish-api");

    public ApiConstruct(
        Construct scope,
        string id,
        EnvironmentConfig config,
        string appOrigin,
        IUserPool userPool,
        IUserPoolClient userPoolClient)
        : base(scope, id)
    {
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
                AllowMethods = [CorsHttpMethod.GET, CorsHttpMethod.POST],
                AllowHeaders = ["authorization", "content-type"],
                MaxAge = Duration.Hours(1),
            },
            Description = $"User file portal API ({config.EnvName})",
        });

        AddRoute(config, "Hello", HttpMethod.GET, "/hello", "HelloFunction");
        AddRoute(config, "Echo", HttpMethod.POST, "/echo", "EchoFunction");
    }

    public HttpApi Api { get; }

    private void AddRoute(
        EnvironmentConfig config, string name, HttpMethod method, string path, string functionClass)
    {
        var function = new Function(this, $"{name}Function", new FunctionProps
        {
            Runtime = Runtime.DOTNET_10,
            Handler = $"Template.Backend::Template.Backend.Functions.{functionClass}_Handle_Generated::Handle",
            Code = Code.FromAsset(Artifact),
            MemorySize = 256,
            Timeout = Duration.Seconds(10),
            LogGroup = new LogGroup(this, $"{name}Logs", new LogGroupProps
            {
                Retention = config.Ephemeral ? RetentionDays.ONE_WEEK : RetentionDays.ONE_MONTH,
                RemovalPolicy = config.Ephemeral ? RemovalPolicy.DESTROY : RemovalPolicy.RETAIN,
            }),
            Description = $"{name} API ({config.EnvName})",
        });

        Api.AddRoutes(new AddRoutesOptions
        {
            Path = path,
            Methods = [method],
            Integration = new HttpLambdaIntegration($"{name}Integration", function),
        });
    }
}
