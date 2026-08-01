namespace Frontend.Components.Layout;

public sealed partial class MainLayout
{
    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public SignOutService SignOut { get; set; } = default!;

    private void SignIn() => Navigation.NavigateToLogin("authentication/login");

    private Task SignOutAsync() => SignOut.SignOutAsync();
}
