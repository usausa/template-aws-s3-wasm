namespace Template.IaC;

// Reads per-environment settings from the cdk.json context. Switch with -c env=dev|prod.
public sealed class EnvironmentConfig
{
    // Target region.
    public const string Region = "ap-northeast-1";

    // Local dev server URL (must match Template.Frontend/Properties/launchSettings.json).
    public const string LocalhostOrigin = "http://localhost:5250";

    private EnvironmentConfig(string envName, string domainPrefix, bool allowLocalhost)
    {
        EnvName = envName;
        DomainPrefix = domainPrefix;
        AllowLocalhost = allowLocalhost;
    }

    public string EnvName { get; }

    // Cognito managed login domain prefix. Must be unique across all AWS accounts,
    // lowercase alphanumerics/hyphens only, and must not contain the reserved words
    // 'aws', 'amazon', or 'cognito' (the service rejects them at creation time).
    public string DomainPrefix { get; }

    // True only for dev: adds localhost to the app client callbacks and the data bucket CORS.
    public bool AllowLocalhost { get; }

    // dev tears down cleanly on stack deletion; prod retains data and users.
    public bool Ephemeral => !String.Equals(EnvName, "prod", StringComparison.Ordinal);

    public static EnvironmentConfig Load(App app, string envName)
    {
        if (app.Node.TryGetContext(envName) is not IDictionary<string, object> context)
        {
            throw new InvalidOperationException($"cdk.json has no context for environment '{envName}'.");
        }

        if (!context.TryGetValue("domainPrefix", out var prefix) || (prefix is not string domainPrefix))
        {
            throw new InvalidOperationException($"domainPrefix is missing for environment '{envName}'.");
        }

        return new EnvironmentConfig(
            envName,
            domainPrefix,
            context.TryGetValue("allowLocalhost", out var allow) && (allow is true));
    }
}
