namespace Template.IaC;

using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.S3;

// Application hosting: private S3 bucket + CloudFront (OAC).
public sealed class HostingConstruct : Construct
{
    public HostingConstruct(Construct scope, string id, EnvironmentConfig config, string apiOriginHost)
        : base(scope, id)
    {
        Bucket = new Bucket(this, "Bucket", new BucketProps
        {
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.S3_MANAGED,
            EnforceSSL = true,
            RemovalPolicy = config.Ephemeral ? RemovalPolicy.DESTROY : RemovalPolicy.RETAIN,
            AutoDeleteObjects = config.Ephemeral
        });

        var headersPolicy = new ResponseHeadersPolicy(this, "Headers", new ResponseHeadersPolicyProps
        {
            SecurityHeadersBehavior = new ResponseSecurityHeadersBehavior
            {
                ContentTypeOptions = new ResponseHeadersContentTypeOptions { Override = true },
                FrameOptions = new ResponseHeadersFrameOptions
                {
                    FrameOption = HeadersFrameOption.DENY,
                    Override = true
                },
                ReferrerPolicy = new ResponseHeadersReferrerPolicy
                {
                    ReferrerPolicy = HeadersReferrerPolicy.STRICT_ORIGIN_WHEN_CROSS_ORIGIN,
                    Override = true
                },
                StrictTransportSecurity = new ResponseHeadersStrictTransportSecurity
                {
                    AccessControlMaxAge = Duration.Days(365),
                    IncludeSubdomains = true,
                    Override = true
                },
                ContentSecurityPolicy = new ResponseHeadersContentSecurityPolicy
                {
                    ContentSecurityPolicy = BuildCsp(config),
                    Override = true
                }
            }
        });

        Distribution = new Distribution(this, "Distribution", new DistributionProps
        {
            DefaultBehavior = new BehaviorOptions
            {
                Origin = S3BucketOrigin.WithOriginAccessControl(Bucket),
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                Compress = true,
                CachePolicy = CachePolicy.CACHING_OPTIMIZED,
                ResponseHeadersPolicy = headersPolicy
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
                    Ttl = Duration.Seconds(10)
                },
                new ErrorResponse
                {
                    HttpStatus = 404,
                    ResponseHttpStatus = 200,
                    ResponsePagePath = "/index.html",
                    Ttl = Duration.Seconds(10)
                }
            ],
            Comment = $"S3 WASM template ({config.EnvName})"
        });

        // Serve the API from the app's own origin. Being same-origin removes the CORS preflight
        // from every call and lets the CSP cover the API with 'self' instead of naming a host.
        // Nothing is cached and the viewer's headers are forwarded, because the Authorization
        // header is what the API Gateway authorizer verifies. The Host header is deliberately
        // not forwarded, so API Gateway still sees its own domain and routes correctly.
        Distribution.AddBehavior($"{ApiConstruct.PathPrefix}/*", new HttpOrigin(apiOriginHost), new AddBehaviorOptions
        {
            ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
            AllowedMethods = AllowedMethods.ALLOW_ALL,
            CachePolicy = CachePolicy.CACHING_DISABLED,
            OriginRequestPolicy = OriginRequestPolicy.ALL_VIEWER_EXCEPT_HOST_HEADER
        });
    }

    public Bucket Bucket { get; }

    public Distribution Distribution { get; }

    // Minimal CSP that lets Blazor WASM run, with connect targets narrowed down.
    //
    // The API needs no entry: it is served from this distribution under /api/*, so 'self' covers
    // it. S3 stays a regional wildcard - the bucket name is generated at deploy time and cannot
    // be referenced from here without a cycle, since the bucket's CORS rule needs this
    // distribution's domain. Pinning it would take a fixed bucket name, and S3 names are global,
    // so a fixed one collides as soon as the same stack id is deployed to a second region.
    //
    // That wildcard is accepted rather than worked around: it still bars every host outside S3 in
    // this region, and the directive that actually stops an injected script from running is
    // script-src, which allows no external or inline source at all.
    private static string BuildCsp(EnvironmentConfig config)
    {
        var cognitoDomain = $"https://{config.DomainPrefix}.auth.{EnvironmentConfig.Region}.amazoncognito.com";
        var connect =
            "'self' " +
            $"https://cognito-idp.{EnvironmentConfig.Region}.amazonaws.com " +
            $"https://cognito-identity.{EnvironmentConfig.Region}.amazonaws.com " +
            $"{cognitoDomain} " +
            $"https://*.s3.{EnvironmentConfig.Region}.amazonaws.com";

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
