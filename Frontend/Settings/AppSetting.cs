namespace Frontend.Settings;

// The App section of wwwroot/appsettings.json.
// Every value here is public by design (public client; authorization is enforced by IAM).
public sealed class AppSetting
{
    // Region of the identity pool and the data bucket.
    public string Region { get; set; } = "ap-northeast-1";

    // User pool used for authentication. Also part of the identity pool Logins key.
    public string UserPoolId { get; set; } = string.Empty;

    // Source of the temporary AWS credentials.
    public string IdentityPoolId { get; set; } = string.Empty;

    // Managed login domain (https://xxx.auth.{region}.amazoncognito.com). Used for sign-out.
    public string CognitoDomain { get; set; } = string.Empty;

    // Bucket holding the per-user data.
    public string DataBucket { get; set; } = string.Empty;

    // Base URL of the authenticated HTTP API (API Gateway).
    public string ApiEndpoint { get; set; } = string.Empty;
}
