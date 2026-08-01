namespace IaC;

// Single-stack layout (the class name avoids the 'Stack' suffix to satisfy CA1711).
// Creation order is Hosting -> Data -> Auth because the data bucket CORS and the
// auth callback URLs both need the distribution domain.
public sealed class Infrastructure : Stack
{
    public Infrastructure(Construct scope, string id, EnvironmentConfig config, IStackProps props)
        : base(scope, id, props)
    {
        var hosting = new HostingConstruct(this, "Hosting", config);

        var appOrigin = $"https://{hosting.Distribution.DistributionDomainName}";

        var data = new DataConstruct(this, "Data", config, appOrigin);
        var auth = new AuthConstruct(this, "Auth", config, appOrigin, data.Bucket);
        var api = new ApiConstruct(this, "Api", config, appOrigin, auth.UserPool, auth.Client);

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
        _ = new CfnOutput(this, "ApiEndpoint", new CfnOutputProps { Value = api.Api.ApiEndpoint! });
    }
}
