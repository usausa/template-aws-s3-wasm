namespace Template.Frontend.Components.Pages;

public sealed partial class Api
{
    //--------------------------------------------------------------------------------
    // State
    //--------------------------------------------------------------------------------

    private bool busy;
    private string? error;

    private HelloResponse? hello;
    private long helloMs;

    private string message = "Hello from the browser";
    private EchoResponse? echo;
    private long echoMs;

    //--------------------------------------------------------------------------------
    // Parameter
    //--------------------------------------------------------------------------------

    [Inject]
    public ApiClient Client { get; set; } = default!;

    [Inject]
    public ILogger<Api> Log { get; set; } = default!;

    //--------------------------------------------------------------------------------
    // Action
    //--------------------------------------------------------------------------------

    private async Task CallHelloAsync()
    {
        var watch = Stopwatch.StartNew();
        await CallAsync(async () =>
        {
            hello = await Client.GetHelloAsync();
            helloMs = watch.ElapsedMilliseconds;
        });
    }

    private async Task CallEchoAsync()
    {
        var watch = Stopwatch.StartNew();
        await CallAsync(async () =>
        {
            echo = await Client.PostEchoAsync(message);
            echoMs = watch.ElapsedMilliseconds;
        });
    }

    // Both calls share the same failure handling: an expired session goes back through sign-in,
    // anything else is surfaced on the page rather than thrown.
    private async Task CallAsync(Func<Task> action)
    {
        busy = true;
        error = null;

        try
        {
            await action();
        }
        catch (AccessTokenNotAvailableException ex)
        {
            ex.Redirect();
        }
        catch (HttpRequestException ex)
        {
            Log.ErrorApiCallFailed(ex);
            error = $"API call failed. ({ex.Message})";
        }
        finally
        {
            busy = false;
        }
    }
}
