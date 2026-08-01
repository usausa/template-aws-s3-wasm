namespace IaC;

using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.S3;

// Application hosting: private S3 bucket + CloudFront (OAC).
public sealed class HostingConstruct : Construct
{
    public HostingConstruct(Construct scope, string id, EnvironmentConfig config)
        : base(scope, id)
    {
        Bucket = new Bucket(this, "Bucket", new BucketProps
        {
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.S3_MANAGED,
            EnforceSSL = true,
            RemovalPolicy = config.Ephemeral ? RemovalPolicy.DESTROY : RemovalPolicy.RETAIN,
            AutoDeleteObjects = config.Ephemeral,
        });

        var headersPolicy = new ResponseHeadersPolicy(this, "Headers", new ResponseHeadersPolicyProps
        {
            SecurityHeadersBehavior = new ResponseSecurityHeadersBehavior
            {
                ContentTypeOptions = new ResponseHeadersContentTypeOptions { Override = true },
                FrameOptions = new ResponseHeadersFrameOptions
                {
                    FrameOption = HeadersFrameOption.DENY,
                    Override = true,
                },
                ReferrerPolicy = new ResponseHeadersReferrerPolicy
                {
                    ReferrerPolicy = HeadersReferrerPolicy.STRICT_ORIGIN_WHEN_CROSS_ORIGIN,
                    Override = true,
                },
                StrictTransportSecurity = new ResponseHeadersStrictTransportSecurity
                {
                    AccessControlMaxAge = Duration.Days(365),
                    IncludeSubdomains = true,
                    Override = true,
                },
                ContentSecurityPolicy = new ResponseHeadersContentSecurityPolicy
                {
                    ContentSecurityPolicy = BuildCsp(config),
                    Override = true,
                },
            },
        });

        Distribution = new Distribution(this, "Distribution", new DistributionProps
        {
            DefaultBehavior = new BehaviorOptions
            {
                Origin = S3BucketOrigin.WithOriginAccessControl(Bucket),
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                Compress = true,
                CachePolicy = CachePolicy.CACHING_OPTIMIZED,
                ResponseHeadersPolicy = headersPolicy,
            },
            DefaultRootObject = "index.html",
            HttpVersion = HttpVersion.HTTP2_AND_3,

            // Include Japan (PRICE_CLASS_100 covers only NA/EU and would route to distant edges).
            PriceClass = PriceClass.PRICE_CLASS_200,

            // SPA fallback. Missing keys come back as 403 through OAC, so map both 403/404 to
            // index.html. Side effect: real access denials on the app bucket also render the app
            // (documented in README).
            ErrorResponses =
            [
                new ErrorResponse
                {
                    HttpStatus = 403,
                    ResponseHttpStatus = 200,
                    ResponsePagePath = "/index.html",
                    Ttl = Duration.Seconds(10),
                },
                new ErrorResponse
                {
                    HttpStatus = 404,
                    ResponseHttpStatus = 200,
                    ResponsePagePath = "/index.html",
                    Ttl = Duration.Seconds(10),
                },
            ],
            Comment = $"S3 WASM template ({config.EnvName})",
        });
    }

    public Bucket Bucket { get; }

    public Distribution Distribution { get; }

    // Minimal CSP that lets Blazor WASM run, with connect targets narrowed down.
    // The S3 and API entries are regional wildcards because the bucket name and API id are only
    // known after deploy (a direct reference would create a circular dependency with the bucket
    // CORS and the API, which is built after hosting).
    // Tighten them to the concrete names after deployment if desired.
    private static string BuildCsp(EnvironmentConfig config)
    {
        var cognitoDomain = $"https://{config.DomainPrefix}.auth.{EnvironmentConfig.Region}.amazoncognito.com";
        var connect =
            "'self' " +
            $"https://cognito-idp.{EnvironmentConfig.Region}.amazonaws.com " +
            $"https://cognito-identity.{EnvironmentConfig.Region}.amazonaws.com " +
            $"{cognitoDomain} " +
            $"https://*.s3.{EnvironmentConfig.Region}.amazonaws.com " +
            $"https://*.execute-api.{EnvironmentConfig.Region}.amazonaws.com";

        return
            "default-src 'self'; " +
            $"connect-src {connect}; " +
            "script-src 'self' 'wasm-unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; " +
            $"frame-src {cognitoDomain}; " +
            "base-uri 'self'; " +
            "form-action 'self'; " +
            "frame-ancestors 'none'; " +
            "object-src 'none'";
    }
}
