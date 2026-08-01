namespace Template.Frontend.Auth;

using System.Text.Json;
using System.Text.Json.Serialization;

// Reads the ID token from the session that the authentication library (oidc-client-ts)
// stores in sessionStorage.
//
// Only an ID token can be passed to GetCredentialsForIdentity, but the C# API of
// Microsoft.AspNetCore.Components.WebAssembly.Authentication exposes access tokens only
// (IAccessTokenProvider). The oidc-client-ts storage key is the stable
// 'oidc.user:{authority}:{clientId}', so it is read directly.
public sealed class OidcTokenAccessor
{
    private readonly IJSRuntime js;
    private readonly string storageKey;

    public OidcTokenAccessor(IJSRuntime js, IConfiguration configuration)
    {
        this.js = js;
        storageKey = $"oidc.user:{configuration["Oidc:Authority"]}:{configuration["Oidc:ClientId"]}";
    }

    // ID token of the current session, or null when signed out.
    public async Task<string?> GetIdTokenAsync()
    {
        var json = await js.InvokeAsync<string?>("sessionStorage.getItem", storageKey);
        if (String.IsNullOrEmpty(json))
        {
            return null;
        }

        var session = JsonSerializer.Deserialize(json, OidcSessionContext.Default.OidcSession);
        return session?.IdToken;
    }

    // Discards the local session on sign-out.
    public async Task ClearSessionAsync() =>
        await js.InvokeVoidAsync("sessionStorage.removeItem", storageKey);

    //--------------------------------------------------------------------------------
    // Identity id cache
    //--------------------------------------------------------------------------------
    //
    // The Cognito identity id never changes for a user, but it lives in memory and is lost on
    // every reload, costing a GetId round trip each time. Caching it in the session removes
    // that call. It is an opaque identifier, not a credential, and it is dropped on sign-out.

    private const string IdentityIdKey = "aws.identityId";

    public async Task<string?> GetCachedIdentityIdAsync() =>
        await js.InvokeAsync<string?>("sessionStorage.getItem", IdentityIdKey);

    public async Task SetCachedIdentityIdAsync(string identityId) =>
        await js.InvokeVoidAsync("sessionStorage.setItem", IdentityIdKey, identityId);

    public async Task ClearCachedIdentityIdAsync() =>
        await js.InvokeVoidAsync("sessionStorage.removeItem", IdentityIdKey);
}

// oidc-client-ts User object persisted in sessionStorage (only the fields we need).
internal sealed record OidcSession(
    [property: JsonPropertyName("id_token")] string? IdToken);

[JsonSerializable(typeof(OidcSession))]
internal sealed partial class OidcSessionContext : JsonSerializerContext;
