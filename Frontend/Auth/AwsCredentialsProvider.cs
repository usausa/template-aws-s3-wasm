namespace Frontend.Auth;

using Amazon;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentity.Model;
using Amazon.Runtime;

// Exchanges the signed-in user's ID token for temporary AWS credentials via the
// Cognito Identity Pool and caches them in memory until they expire.
//
// GetId / GetCredentialsForIdentity require no SigV4 signature, so an anonymous client
// is used. The permissions of the issued credentials are restricted to users/{sub}/
// by the IAM policy defined on the IaC side.
public sealed class AwsCredentialsProvider : IDisposable
{
    // Margin so S3 requests never start with nearly-expired credentials.
    private static readonly TimeSpan ExpirationMargin = TimeSpan.FromMinutes(5);

    private readonly IAccessTokenProvider tokenProvider;
    private readonly OidcTokenAccessor tokenAccessor;
    private readonly AppSetting setting;
    private readonly ILogger<AwsCredentialsProvider> log;
    private readonly SemaphoreSlim gate = new(1, 1);

    private string? identityId;
    private SessionAWSCredentials? credentials;
    private DateTime expiration;

    public AwsCredentialsProvider(
        IAccessTokenProvider tokenProvider,
        OidcTokenAccessor tokenAccessor,
        AppSetting setting,
        ILogger<AwsCredentialsProvider> log)
    {
        this.tokenProvider = tokenProvider;
        this.tokenAccessor = tokenAccessor;
        this.setting = setting;
        this.log = log;
    }

    public void Dispose() => gate.Dispose();

    // Returns valid temporary credentials, or null when signed out / session expired
    // (the caller redirects to interactive login).
    public async Task<SessionAWSCredentials?> GetCredentialsAsync()
    {
        await gate.WaitAsync();
        try
        {
            if ((credentials is not null) && (DateTime.UtcNow < expiration - ExpirationMargin))
            {
                return credentials;
            }

            // A silent refresh runs here when the session is about to expire, which also
            // renews the sessionStorage copy. When refresh is impossible (third-party
            // cookie restrictions etc.) null is returned instead.
            var token = await tokenProvider.RequestAccessToken();
            if (token.Status != AccessTokenResultStatus.Success)
            {
                log.WarnTokenUnavailable(token.Status.ToString());
                return null;
            }

            var idToken = await tokenAccessor.GetIdTokenAsync();
            if (idToken is null)
            {
                log.WarnTokenUnavailable("IdTokenMissing");
                return null;
            }

            using var client = new AmazonCognitoIdentityClient(
                new AnonymousAWSCredentials(), RegionEndpoint.GetBySystemName(setting.Region));

            var logins = new Dictionary<string, string>
            {
                [$"cognito-idp.{setting.Region}.amazonaws.com/{setting.UserPoolId}"] = idToken,
            };

            // The IdentityId never changes for a user, so resolve it only once.
            identityId ??= (await client.GetIdAsync(new GetIdRequest
            {
                IdentityPoolId = setting.IdentityPoolId,
                Logins = logins,
            })).IdentityId;

            var response = await client.GetCredentialsForIdentityAsync(new GetCredentialsForIdentityRequest
            {
                IdentityId = identityId,
                Logins = logins,
            });

            credentials = new SessionAWSCredentials(
                response.Credentials.AccessKeyId,
                response.Credentials.SecretKey,
                response.Credentials.SessionToken);

            // Credentials last about an hour. Fall back to a safe default when the
            // server value is missing.
            var serverExpiration = response.Credentials.Expiration;
            expiration = serverExpiration.HasValue
                ? serverExpiration.Value.ToUniversalTime()
                : DateTime.UtcNow.AddMinutes(50);

            log.InfoCredentialsAcquired(identityId, expiration);
            return credentials;
        }
        finally
        {
            gate.Release();
        }
    }

    // Called on sign-out to drop the cache deterministically.
    public void Clear()
    {
        credentials = null;
        identityId = null;
        expiration = DateTime.MinValue;
    }
}
