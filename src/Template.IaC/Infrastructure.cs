namespace Template.IaC;

// Single-stack layout (the class name avoids the 'Stack' suffix to satisfy CA1711).
//
// The creation order is forced by a chain of references, each of which is one-way:
//   API (bare)   - depends on nothing here
//   Hosting      - needs the API host for its /api/* origin
//   Data, Auth   - need the distribution domain for the bucket CORS and the callback URLs
//   API routes   - need the user pool client for the authorizer
// Creating the API in two steps is what keeps this acyclic; see ApiConstruct.
public sealed class Infrastructure : Stack
{
    public Infrastructure(Construct scope, string id, EnvironmentConfig config, IStackProps props)
        : base(scope, id, props)
    {
        var api = new ApiConstruct(this, "Api", config);

        var hosting = new HostingConstruct(this, "Hosting", config, api.OriginHost);

        var appOrigin = $"https://{hosting.Distribution.DistributionDomainName}";

        var data = new DataConstruct(this, "Data", config, appOrigin);
        var auth = new AuthConstruct(this, "Auth", config, appOrigin, data.Bucket);

        api.AddRoutes(auth.UserPool, auth.Client);

        //--------------------------------------------------------------------------------
        // Outputs (consumed by scripts/update-appsettings.ps1 and deploy-app.ps1)
        //--------------------------------------------------------------------------------

        _ = new CfnOutput(this, "CloudFrontDomain", new CfnOutputProps { Value = hosting.Distribution.DistributionDomainName });
        _ = new CfnOutput(this, "DistributionId", new CfnOutputProps { Value = hosting.Distribution.DistributionId });
        _ = new CfnOutput(this, "AppBucketName", new CfnOutputProps { Value = hosting.Bucket.BucketName });
        _ = new CfnOutput(this, "DataBucketName", new CfnOutputProps { Value = data.Bucket.BucketName });
        _ = new CfnOutput(this, "UserPoolId", new CfnOutputProps { Value = auth.UserPool.UserPoolId });
        _ = new CfnOutput(this, "UserPoolClientId", new CfnOutputProps { Value = auth.Client.UserPoolClientId });
        _ = new CfnOutput(this, "CognitoDomain", new CfnOutputProps { Value = $"https://{config.DomainPrefix}.auth.{EnvironmentConfig.Region}.amazoncognito.com" });
        _ = new CfnOutput(this, "IdentityPoolId", new CfnOutputProps { Value = auth.IdentityPoolId });
        // The app talks to the API through CloudFront, so this is the app origin plus the prefix,
        // not the regional execute-api endpoint.
        _ = new CfnOutput(this, "ApiEndpoint", new CfnOutputProps { Value = $"{appOrigin}{ApiConstruct.PathPrefix}" });
    }
}
