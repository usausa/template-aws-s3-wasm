namespace Template.Frontend.Auth;

// Cognito's /logout endpoint does not support OIDC RP-initiated logout
// (end_session_endpoint); it only accepts client_id + logout_uri. The standard sign-out
// flow of the authentication library therefore cannot be used.
// This service discards the local session and credential cache, then navigates to the
// managed login /logout endpoint. The logout_uri (app root URL) is registered as an
// allowed sign-out URL on the Template.IaC side.
public sealed class SignOutService
{
    private readonly NavigationManager navigation;
    private readonly OidcTokenAccessor tokenAccessor;
    private readonly AwsCredentialsProvider credentialsProvider;
    private readonly AppSetting setting;
    private readonly string clientId;

    public SignOutService(
        NavigationManager navigation,
        OidcTokenAccessor tokenAccessor,
        AwsCredentialsProvider credentialsProvider,
        AppSetting setting,
        IConfiguration configuration)
    {
        this.navigation = navigation;
        this.tokenAccessor = tokenAccessor;
        this.credentialsProvider = credentialsProvider;
        this.setting = setting;
        clientId = configuration["Oidc:ClientId"] ?? string.Empty;
    }

    public async Task SignOutAsync()
    {
        credentialsProvider.Clear();
        await tokenAccessor.ClearCachedIdentityIdAsync();
        await tokenAccessor.ClearSessionAsync();

        var logoutUri = Uri.EscapeDataString(navigation.BaseUri);
        navigation.NavigateTo(
            $"{setting.CognitoDomain}/logout?client_id={clientId}&logout_uri={logoutUri}",
            forceLoad: true);
    }
}
