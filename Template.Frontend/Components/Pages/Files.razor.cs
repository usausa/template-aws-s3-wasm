namespace Template.Frontend.Components.Pages;

using Amazon.Runtime;

using Template.Frontend.Application;

public sealed partial class Files
{
    // Files above this size are not fetched into the browser at all.
    private const long ContentLimit = 1024 * 1024;

    //--------------------------------------------------------------------------------
    // State
    //--------------------------------------------------------------------------------

    private List<UserFile>? files;
    private bool loading = true;
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
            var result = await Repository.ListAsync(sub);
            if (result is null)
            {
                // Session expired. Redirect to interactive login.
                Navigation.NavigateToLogin("authentication/login");
                return;
            }

            files = result;

            // Open the first renderable file so the page is not empty on arrival.
            var first = files.FirstOrDefault(static x => MediaHelper.IsPreviewableText(x.Name));
            if (first is not null)
            {
                await SelectAsync(first);
            }
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

    private async Task SelectAsync(UserFile file)
    {
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
            var content = await Repository.GetTextAsync(file.Key);
            if (content is null)
            {
                Navigation.NavigateToLogin("authentication/login");
                return;
            }

            // Anything that is not a recognised series falls back to the raw text view.
            series = SeriesParser.Parse(file.Name, content);
            text = series is null ? content : null;
        }
        catch (AmazonClientException ex)
        {
            Log.ErrorFileOperation(nameof(SelectAsync), ex);
            error = $"Failed to load the file. ({ex.Message})";
        }
        catch (HttpRequestException ex)
        {
            Log.ErrorFileOperation(nameof(SelectAsync), ex);
            error = $"Failed to load the file. ({ex.Message})";
        }
        finally
        {
            loadingDetail = false;
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
