namespace Template.IaC;

using Amazon.CDK.AWS.S3;

// Per-user data bucket.
// An external system writes objects under users/{sub}/... and Template.Frontend reads them directly
// with SigV4-signed requests. Reads are cross-origin browser requests, so CORS is enabled
// and restricted to the app origin.
public sealed class DataConstruct : Construct
{
    public DataConstruct(Construct scope, string id, EnvironmentConfig config, string appOrigin)
        : base(scope, id)
    {
        var origins = new List<string> { appOrigin };
        if (config.AllowLocalhost)
        {
            origins.Add(EnvironmentConfig.LocalhostOrigin);
        }

        Bucket = new Bucket(this, "Bucket", new BucketProps
        {
            // Name left to CDK. A fixed name would let the CSP list this exact host, but S3 names
            // are global, so a fixed one collides as soon as the same stack id is deployed to a
            // second region. Not worth it for the marginal CSP gain (see HostingConstruct).
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.S3_MANAGED,
            EnforceSSL = true,
            RemovalPolicy = config.Ephemeral ? RemovalPolicy.DESTROY : RemovalPolicy.RETAIN,
            AutoDeleteObjects = config.Ephemeral,
            Cors =
            [
                new CorsRule
                {
                    AllowedOrigins = [.. origins],
                    AllowedMethods = [HttpMethods.GET, HttpMethods.HEAD],
                    AllowedHeaders = ["*"],
                    ExposedHeaders = ["ETag"],
                    MaxAge = 3000,
                },
            ],
        });
    }

    public Bucket Bucket { get; }
}
