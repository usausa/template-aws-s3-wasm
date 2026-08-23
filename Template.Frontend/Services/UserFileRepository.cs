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

    // Lists the caller's files. Returns null when credentials are unavailable
    // (the caller redirects to login).
    public async Task<List<UserFile>?> ListAsync(string sub)
    {
        var credentials = await credentialsProvider.GetCredentialsAsync();
        if (credentials is null)
        {
            return null;
        }

        var watch = Stopwatch.StartNew();
        var prefix = Prefix(sub);
        var s3 = ResolveClient(credentials);

        var files = new List<UserFile>();
        var request = new ListObjectsV2Request
        {
            BucketName = setting.DataBucket,
            Prefix = prefix,
        };

        ListObjectsV2Response response;
        do
        {
            response = await s3.ListObjectsV2Async(request);
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

            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated == true);

        files.Sort(static (x, y) => String.CompareOrdinal(x.Key, y.Key));

        log.InfoFilesListed(files.Count, prefix, watch.ElapsedMilliseconds);
        return files;
    }

    // Reads a text file. Returns null when credentials are unavailable.
    public async Task<string?> GetTextAsync(string key)
    {
        var credentials = await credentialsProvider.GetCredentialsAsync();
        if (credentials is null)
        {
            return null;
        }

        var s3 = ResolveClient(credentials);
        using var response = await s3.GetObjectAsync(setting.DataBucket, key);
        using var reader = new StreamReader(response.ResponseStream);
        return await reader.ReadToEndAsync();
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
