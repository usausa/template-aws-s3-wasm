namespace Template.IaC;

using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AwsApigatewayv2Authorizers;
using Amazon.CDK.AwsApigatewayv2Integrations;

// Both the API and Lambda namespaces define HttpMethod.
using HttpMethod = Amazon.CDK.AWS.Apigatewayv2.HttpMethod;

// Authenticated API: CloudFront /api/* -> HTTP API -> Cognito JWT authorizer -> Lambda.
//
// The authorizer is attached to every route, so an unauthenticated call is rejected with 401
// before any function is invoked. That is the reason the handlers carry no authorization logic
// of their own: by the time a request reaches them, the token is verified and its claims can be
// trusted.
//
// Authorization here is token-based, unlike the S3 path which uses IAM with temporary
// credentials. Both start from the same Cognito sign-in; they differ in what evaluates the
// permission (API Gateway verifying a JWT vs. S3 evaluating an IAM policy).
//
// Routing lives here rather than in [HttpApi] annotations so that the whole infrastructure stays
// described in one place. The handler names below are produced by the Lambda Annotations source
// generator: {Assembly}::{Namespace}.{Class}_{Method}_Generated::{Method}.
//
// Construction is split in two because the resources form a chain that only resolves in this
// order: the distribution needs the API's host for its /api/* origin, the user pool client needs
// the distribution's domain for its callback URLs, and the authorizer needs that client. Creating
// the API bare first and attaching the authorizer afterwards keeps that chain acyclic - the API
// resource itself depends on nothing here.
public sealed class ApiConstruct : Construct
{
    // Paths are prefixed so CloudFront can forward /api/* through unchanged.
    public const string PathPrefix = "/api";

    // Published output of the Template.Backend project, produced by scripts/deploy-api.ps1. Every
    // function shares this one artifact and differs only by handler.
    private static readonly string Artifact =
        System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "publish-api");

    private readonly EnvironmentConfig config;

    public ApiConstruct(Construct scope, string id, EnvironmentConfig config)
        : base(scope, id)
    {
        this.config = config;

        // No CORS configuration: the browser reaches this API through CloudFront on the app's own
        // origin, so requests are same-origin and never trigger a preflight.
        Api = new HttpApi(this, "Api", new HttpApiProps
        {
            Description = $"User file portal API ({config.EnvName})",
        });
    }

    public HttpApi Api { get; }

    // Regional endpoint host, used as the CloudFront origin for /api/*.
    public string OriginHost => $"{Api.ApiId}.execute-api.{EnvironmentConfig.Region}.amazonaws.com";

    // Called once the user pool client exists. Every route is added with the authorizer, so no
    // route can be reached without a valid token.
    public void AddRoutes(IUserPool userPool, IUserPoolClient userPoolClient)
    {
        var authorizer = new HttpUserPoolAuthorizer("Authorizer", userPool, new HttpUserPoolAuthorizerProps
        {
            UserPoolClients = [userPoolClient],

            // The app calls the API with an access token, whose audience lives in the
            // 'client_id' claim rather than 'aud'.
            IdentitySource = ["$request.header.Authorization"],
        });

        AddRoute(authorizer, "Hello", HttpMethod.GET, "/hello", "HelloFunction");
        AddRoute(authorizer, "Echo", HttpMethod.POST, "/echo", "EchoFunction");
    }

    private void AddRoute(
        IHttpRouteAuthorizer authorizer, string name, HttpMethod method, string path, string functionClass)
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
            Path = $"{PathPrefix}{path}",
            Methods = [method],
            Authorizer = authorizer,
            Integration = new HttpLambdaIntegration($"{name}Integration", function),
        });
    }
}
