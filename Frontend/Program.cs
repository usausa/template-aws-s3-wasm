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
});

// Components
builder.Services.AddScoped<OidcTokenAccessor>();
builder.Services.AddScoped<AwsCredentialsProvider>();
builder.Services.AddScoped<SignOutService>();
builder.Services.AddScoped<UserFileRepository>();

await builder.Build().RunAsync();
