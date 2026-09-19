namespace Template.Frontend.Components.Pages;

using Amazon.Runtime;

using Template.Frontend.Application;

public sealed partial class Files : IDisposable
{
    // Files above this size are not fetched into the browser at all.
    private const long ContentLimit = 1024 * 1024;

    //--------------------------------------------------------------------------------
    // State
    //--------------------------------------------------------------------------------

    // Cancels requests still in flight when the page is left.
    private readonly CancellationTokenSource lifetimeCts = new();

    // A newer selection cancels the previous read so a slow response cannot overwrite the current file.
    private CancellationTokenSource? selectCts;

    private List<UserFile>? files;
    private string? nextToken;
    private bool loading = true;
    private bool loadingMore;
    private bool loadingDetail;
    private string? error;
    private UserFile? selected;
    private DataSeries? series;
    private string? text;
    private string sub = string.Empty;

    //--------------------------------------------------------------------------------
    // Parameter
    //--------------------------------------------------------------------------------

    [CascadingParameter]
    public Task<AuthenticationState> AuthenticationStateTask { get; set; } = default!;

    [Inject]
    public UserFileRepository Repository { get; set; } = default!;

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public ILogger<Files> Log { get; set; } = default!;

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthenticationStateTask;
        sub = state.User.FindFirst("sub")?.Value ?? string.Empty;

        await ReloadAsync();
    }

    public void Dispose()
    {
        selectCts?.Cancel();
        selectCts?.Dispose();
        lifetimeCts.Cancel();
        lifetimeCts.Dispose();
    }

    //--------------------------------------------------------------------------------
    // Action
    //--------------------------------------------------------------------------------

    private async Task ReloadAsync()
    {
        loading = true;
        error = null;
        ClearDetail();

        try
        {
            var page = await Repository.ListAsync(sub, null, lifetimeCts.Token);
            if (page is null)
            {
                // Session expired. Redirect to interactive login.
                Navigation.NavigateToLogin("authentication/login");
                return;
            }

            files = [.. page.Files];
            nextToken = page.ContinuationToken;

            // Open the first renderable file so the page is not empty on arrival.
            var first = files.FirstOrDefault(static x => MediaHelper.IsPreviewableText(x.Name));
            if (first is not null)
            {
                await SelectAsync(first);
            }
        }
        catch (OperationCanceledException)
        {
            // The page was left while loading.
        }
        catch (AmazonClientException ex)
        {
            // AmazonServiceException also derives from AmazonClientException.
            Log.ErrorFileOperation(nameof(ReloadAsync), ex);
            error = $"Failed to list files. ({ex.Message})";
        }
        catch (HttpRequestException ex)
        {
            Log.ErrorFileOperation(nameof(ReloadAsync), ex);
            error = $"Failed to list files. ({ex.Message})";
        }
        finally
        {
            loading = false;
        }
    }

    private async Task LoadMoreAsync()
    {
        if ((files is null) || (nextToken is null) || loadingMore)
        {
            return;
        }

        loadingMore = true;
        try
        {
            var page = await Repository.ListAsync(sub, nextToken, lifetimeCts.Token);
            if (page is null)
            {
                Navigation.NavigateToLogin("authentication/login");
                return;
            }

            files.AddRange(page.Files);
            nextToken = page.ContinuationToken;
        }
        catch (OperationCanceledException)
        {
            // The page was left while loading.
        }
        catch (AmazonClientException ex)
        {
            Log.ErrorFileOperation(nameof(LoadMoreAsync), ex);
            error = $"Failed to list files. ({ex.Message})";
        }
        catch (HttpRequestException ex)
        {
            Log.ErrorFileOperation(nameof(LoadMoreAsync), ex);
            error = $"Failed to list files. ({ex.Message})";
        }
        finally
        {
            loadingMore = false;
        }
    }

    private async Task SelectAsync(UserFile file)
    {
        if (selectCts is not null)
        {
            await selectCts.CancelAsync();
            selectCts.Dispose();
        }

        selectCts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCts.Token);
        var token = selectCts.Token;

        selected = file;
        series = null;
        text = null;

        if (!MediaHelper.IsPreviewableText(file.Name) || (file.Size > ContentLimit))
        {
            return;
        }

        loadingDetail = true;
        try
        {
            var content = await Repository.GetTextAsync(file.Key, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (content is null)
            {
                Navigation.NavigateToLogin("authentication/login");
                return;
            }

            // Anything that is not a recognised series falls back to the raw text view.
            series = SeriesParser.Parse(file.Name, content);
            text = series is null ? content : null;
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer selection, or the page was left.
        }
        catch (AmazonClientException ex) when (!token.IsCancellationRequested)
        {
            Log.ErrorFileOperation(nameof(SelectAsync), ex);
            error = $"Failed to load the file. ({ex.Message})";
        }
        catch (HttpRequestException ex) when (!token.IsCancellationRequested)
        {
            Log.ErrorFileOperation(nameof(SelectAsync), ex);
            error = $"Failed to load the file. ({ex.Message})";
        }
        finally
        {
            // A superseded request must not clear the indicator of the newer one.
            if (!token.IsCancellationRequested)
            {
                loadingDetail = false;
            }
        }
    }

    private void ClearDetail()
    {
        selected = null;
        series = null;
        text = null;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static string FormatValue(double value) =>
        value.ToString("N1", CultureInfo.InvariantCulture);
}
