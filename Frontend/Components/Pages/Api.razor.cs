namespace Frontend.Components.Pages;

public sealed partial class Api
{
    //--------------------------------------------------------------------------------
    // State
    //--------------------------------------------------------------------------------

    private bool calling;
    private string? error;
    private HelloResponse? response;
    private long elapsedMs;

    //--------------------------------------------------------------------------------
    // Parameter
    //--------------------------------------------------------------------------------

    [Inject]
    public ApiClient Client { get; set; } = default!;

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public ILogger<Api> Log { get; set; } = default!;

    //--------------------------------------------------------------------------------
    // Action
    //--------------------------------------------------------------------------------

    private async Task CallAsync()
    {
        calling = true;
        error = null;
        response = null;

        var watch = Stopwatch.StartNew();
        try
        {
            response = await Client.GetHelloAsync();
            elapsedMs = watch.ElapsedMilliseconds;
        }
        catch (AccessTokenNotAvailableException ex)
        {
            // The session expired before the call; send the user back through sign-in.
            ex.Redirect();
        }
        catch (HttpRequestException ex)
        {
            Log.ErrorApiCallFailed(ex);
            error = $"API call failed. ({ex.Message})";
        }
        finally
        {
            calling = false;
        }
    }
}
