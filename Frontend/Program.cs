using Amazon;

using Frontend.Application;

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

//--------------------------------------------------------------------------------
// Configure builder
//--------------------------------------------------------------------------------

// The AWS SDK HTTP stack needs browser-specific handling (see BrowserHttpClientFactory).
AWSConfigs.HttpClientFactory = new BrowserHttpClientFactory();

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Setting (wwwroot/appsettings.json is loaded automatically by CreateDefault)
var setting = builder.Configuration.GetSection("App").Get<AppSetting>() ?? new AppSetting();
builder.Services.AddSingleton(setting);

// Authentication: Cognito User Pool as the OIDC provider (authorization code + PKCE).
// Authority / ClientId / ResponseType come from the Oidc section of appsettings.json.
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Oidc", options.ProviderOptions);

    // The default scopes are openid / profile. email is added here instead of in the
    // settings file because configuration binding would append to the defaults.
    options.ProviderOptions.DefaultScopes.Add("email");

    // The OIDC discovery request cannot be avoided: OidcProviderOptions only accepts string
    // values, so the metadata document cannot be supplied inline. index.html preconnects to
    // the Cognito hosts instead, which removes DNS + TLS setup from that request.
});

// API client. The handler attaches the access token only to URLs under the API endpoint, so it
// never travels to S3 or Cognito.
builder.Services.AddScoped(sp =>
{
    var handler = new AuthorizationMessageHandler(
        sp.GetRequiredService<IAccessTokenProvider>(),
        sp.GetRequiredService<NavigationManager>());
    return handler.ConfigureHandler(authorizedUrls: [setting.ApiEndpoint]);
});

builder.Services
    .AddHttpClient(ApiClient.Name, client => client.BaseAddress = new Uri(setting.ApiEndpoint + "/"))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();

// Components
builder.Services.AddScoped<OidcTokenAccessor>();
builder.Services.AddScoped<AwsCredentialsProvider>();
builder.Services.AddScoped<SignOutService>();
builder.Services.AddScoped<UserFileRepository>();
builder.Services.AddScoped<ApiClient>();

await builder.Build().RunAsync();
