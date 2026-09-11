namespace Template.Frontend.Components;

// Sends unauthenticated visitors of [Authorize] pages to the login flow.
// The current URL is carried over as the return URL.
public sealed class RedirectToLogin : ComponentBase
{
    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    protected override void OnInitialized() =>
        Navigation.NavigateToLogin("authentication/login");
}
