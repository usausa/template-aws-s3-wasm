namespace Template.IaC;

public static class Program
{
    public static void Main()
    {
        var app = new App();

        var envName = app.Node.TryGetContext("env") as string ?? "dev";
        var config = EnvironmentConfig.Load(app, envName);

        _ = new Infrastructure(app, $"template-aws-s3-wasm-{envName}", config, new StackProps
        {
            Env = new Amazon.CDK.Environment
            {
                Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                Region = EnvironmentConfig.Region
            },
            Description = "S3 hosted WASM application template (CloudFront hosting + Cognito auth + per-user data bucket)"
        });

        app.Synth();
    }
}
