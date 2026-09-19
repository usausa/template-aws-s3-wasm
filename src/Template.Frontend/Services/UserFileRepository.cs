namespace Template.Frontend.Services;

using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

using Template.Frontend.Auth;

// Reads the users/{sub}/ prefix of the data bucket.
//
// The sub here is only for key construction and display; the security boundary is IAM.
// The temporary credentials carry permissions for the caller's own prefix only, so
// tampering with keys on the client cannot reach other users' data (AccessDenied).
public sealed class UserFileRepository : IDisposable
{
    // Keys per list request. The page renders the first page immediately and asks for the rest on demand.
    private const int ListPageSize = 100;

    private readonly AwsCredentialsProvider credentialsProvider;
    private readonly AppSetting setting;
    private readonly ILogger<UserFileRepository> log;

    // The client is kept alive across calls and only rebuilt when the credentials are renewed.
    // Constructing one per request repeats endpoint and signer setup for no benefit.
    private AmazonS3Client? client;
    private SessionAWSCredentials? clientCredentials;

    public UserFileRepository(
        AwsCredentialsProvider credentialsProvider,
        AppSetting setting,
        ILogger<UserFileRepository> log)
    {
        this.credentialsProvider = credentialsProvider;
        this.setting = setting;
        this.log = log;
    }

    public void Dispose() => client?.Dispose();

    public static string Prefix(string sub) => $"users/{sub}/";

    // Lists one page of the caller's files (S3 returns keys in byte order, so no sorting is needed).
    // Pass the previous page's ContinuationToken to continue. Returns null when credentials are
    // unavailable (the caller redirects to login).
    public async Task<UserFilePage?> ListAsync(string sub, string? continuationToken, CancellationToken cancellationToken)
    {
        var credentials = await credentialsProvider.GetCredentialsAsync();
        if (credentials is null)
        {
            return null;
        }

        var watch = Stopwatch.StartNew();
        var prefix = Prefix(sub);
        var s3 = ResolveClient(credentials);

        var request = new ListObjectsV2Request
        {
            BucketName = setting.DataBucket,
            Prefix = prefix,
            MaxKeys = ListPageSize,
            ContinuationToken = continuationToken,
        };
        var response = await s3.ListObjectsV2Async(request, cancellationToken);

        var files = new List<UserFile>();
        foreach (var s3Object in response.S3Objects ?? [])
        {
            // Skip the zero-byte object representing the prefix itself (folder placeholder).
            if (String.Equals(s3Object.Key, prefix, StringComparison.Ordinal))
            {
                continue;
            }

            files.Add(new UserFile(
                s3Object.Key,
                s3Object.Key[prefix.Length..],
                s3Object.Size ?? 0,
                s3Object.LastModified));
        }

        log.InfoFilesListed(files.Count, prefix, watch.ElapsedMilliseconds);
        return new UserFilePage(files, response.IsTruncated == true ? response.NextContinuationToken : null);
    }

    // Reads a text file. Returns null when credentials are unavailable.
    public async Task<string?> GetTextAsync(string key, CancellationToken cancellationToken)
    {
        var credentials = await credentialsProvider.GetCredentialsAsync();
        if (credentials is null)
        {
            return null;
        }

        var s3 = ResolveClient(credentials);
        using var response = await s3.GetObjectAsync(setting.DataBucket, key, cancellationToken);
        using var reader = new StreamReader(response.ResponseStream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    // Plain S3 object URL, shown in the UI so the access model can be checked by hand.
    // Opening it in a browser always fails with AccessDenied: the bucket blocks public
    // access, so reads require a SigV4-signed request carrying the caller's credentials.
    // Each segment is escaped individually so the key separators survive.
    public Uri ObjectUrl(string key) =>
        new($"https://{setting.DataBucket}.s3.{setting.Region}.amazonaws.com/" +
            String.Join('/', key.Split('/').Select(Uri.EscapeDataString)));

    private AmazonS3Client ResolveClient(SessionAWSCredentials credentials)
    {
        if (!ReferenceEquals(clientCredentials, credentials) || (client is null))
        {
            client?.Dispose();
            client = new AmazonS3Client(credentials, RegionEndpoint.GetBySystemName(setting.Region));
            clientCredentials = credentials;
        }

        return client;
    }
}
