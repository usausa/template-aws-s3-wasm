namespace Template.IaC;

using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;

// Authentication (User Pool + managed login) and authorization (Identity Pool -> IAM role).
//
// The identity pool pieces are built with L1 (Cfn*) resources on purpose so the mechanism
// stays visible:
//   - CfnIdentityPool: authenticated identities only, backed by this user pool client
//   - CfnIdentityPoolPrincipalTag: maps the ID token 'sub' claim to the session tag 'userId'
//   - CfnIdentityPoolRoleAttachment: assigns the authenticated role
public sealed class AuthConstruct : Construct
{
    public AuthConstruct(Construct scope, string id, EnvironmentConfig config, string appOrigin, IBucket dataBucket)
        : base(scope, id)
    {
        //--------------------------------------------------------------------------------
        // User Pool (authentication)
        //--------------------------------------------------------------------------------

        UserPool = new UserPool(this, "UserPool", new UserPoolProps
        {
            // Users are admin-issued only: per-user data is provisioned by a separate
            // system, so self sign-up makes no sense here.
            SelfSignUpEnabled = false,
            SignInAliases = new SignInAliases { Email = true },
            StandardAttributes = new StandardAttributes
            {
                Email = new StandardAttribute { Required = true, Mutable = true },
            },
            AccountRecovery = AccountRecovery.EMAIL_ONLY,
            FeaturePlan = FeaturePlan.ESSENTIALS,
            RemovalPolicy = config.Ephemeral ? RemovalPolicy.DESTROY : RemovalPolicy.RETAIN,
            DeletionProtection = !config.Ephemeral,
        });

        var callbackUrls = new List<string> { $"{appOrigin}/authentication/login-callback" };
        var logoutUrls = new List<string> { $"{appOrigin}/" };
        if (config.AllowLocalhost)
        {
            callbackUrls.Add($"{EnvironmentConfig.LocalhostOrigin}/authentication/login-callback");
            logoutUrls.Add($"{EnvironmentConfig.LocalhostOrigin}/");
        }

        Client = UserPool.AddClient("Client", new UserPoolClientOptions
        {
            // Public client (a WASM app cannot keep a secret). Authorization code + PKCE.
            GenerateSecret = false,
            AuthFlows = new AuthFlow { UserSrp = true },
            OAuth = new OAuthSettings
            {
                Flows = new OAuthFlows { AuthorizationCodeGrant = true },
                Scopes = [OAuthScope.OPENID, OAuthScope.EMAIL, OAuthScope.PROFILE],
                CallbackUrls = callbackUrls.ToArray(),
                LogoutUrls = logoutUrls.ToArray(),
            },
            PreventUserExistenceErrors = true,
            AccessTokenValidity = Duration.Minutes(60),
            IdTokenValidity = Duration.Minutes(60),
            RefreshTokenValidity = Duration.Days(30),
        });

        Domain = UserPool.AddDomain("Domain", new UserPoolDomainOptions
        {
            CognitoDomain = new CognitoDomainOptions { DomainPrefix = config.DomainPrefix },
            ManagedLoginVersion = ManagedLoginVersion.NEWER_MANAGED_LOGIN,
        });

        // Managed login (v2) requires a branding definition per client; assign the default style.
        _ = new CfnManagedLoginBranding(this, "Branding", new CfnManagedLoginBrandingProps
        {
            UserPoolId = UserPool.UserPoolId,
            ClientId = Client.UserPoolClientId,
            UseCognitoProvidedValues = true,
        });

        //--------------------------------------------------------------------------------
        // Identity Pool (authorization)
        //--------------------------------------------------------------------------------

        var providerName = $"cognito-idp.{EnvironmentConfig.Region}.amazonaws.com/{UserPool.UserPoolId}";

        var identityPool = new CfnIdentityPool(this, "IdentityPool", new CfnIdentityPoolProps
        {
            AllowUnauthenticatedIdentities = false,
            CognitoIdentityProviders = new[]
            {
                new CfnIdentityPool.CognitoIdentityProviderProperty
                {
                    ProviderName = providerName,
                    ClientId = Client.UserPoolClientId,
                    ServerSideTokenCheck = true,
                },
            },
        });

        // Propagate the ID token 'sub' (user pool user id) as the session tag 'userId'.
        // IAM policies below use ${aws:PrincipalTag/userId} to allow only the user's own prefix.
        // The classic ${cognito-identity.amazonaws.com:sub} (IdentityId) variable is not used
        // because the IdentityId is only assigned on first login and cannot be correlated by
        // the system that provisions the data (see README.md).
        _ = new CfnIdentityPoolPrincipalTag(this, "PrincipalTags", new CfnIdentityPoolPrincipalTagProps
        {
            IdentityPoolId = identityPool.Ref,
            IdentityProviderName = providerName,
            UseDefaults = false,
            PrincipalTags = new Dictionary<string, string> { ["userId"] = "sub" },
        });

        // Role assumed by authenticated users. sts:TagSession is required for session tags.
        var authenticatedRole = new Role(this, "AuthenticatedRole", new RoleProps
        {
            AssumedBy = new FederatedPrincipal(
                "cognito-identity.amazonaws.com",
                new Dictionary<string, object>
                {
                    ["StringEquals"] = new Dictionary<string, object>
                    {
                        ["cognito-identity.amazonaws.com:aud"] = identityPool.Ref,
                    },
                    ["ForAnyValue:StringLike"] = new Dictionary<string, object>
                    {
                        ["cognito-identity.amazonaws.com:amr"] = "authenticated",
                    },
                },
                "sts:AssumeRoleWithWebIdentity").WithSessionTags(),
            Description = "Authenticated role for the user file portal",
        });

        authenticatedRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "ListOwnPrefix",
            Actions = ["s3:ListBucket"],
            Resources = [dataBucket.BucketArn],
            Conditions = new Dictionary<string, object>
            {
                ["StringLike"] = new Dictionary<string, object>
                {
                    ["s3:prefix"] = "users/${aws:PrincipalTag/userId}/*",
                },
            },
        }));

        authenticatedRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "GetOwnObjects",
            Actions = ["s3:GetObject"],
            Resources = [dataBucket.BucketArn + "/users/${aws:PrincipalTag/userId}/*"],
        }));

        _ = new CfnIdentityPoolRoleAttachment(this, "RoleAttachment", new CfnIdentityPoolRoleAttachmentProps
        {
            IdentityPoolId = identityPool.Ref,
            Roles = new Dictionary<string, object> { ["authenticated"] = authenticatedRole.RoleArn },
        });

        IdentityPoolId = identityPool.Ref;
    }

    public UserPool UserPool { get; }

    public UserPoolClient Client { get; }

    public UserPoolDomain Domain { get; }

    public string IdentityPoolId { get; }
}
