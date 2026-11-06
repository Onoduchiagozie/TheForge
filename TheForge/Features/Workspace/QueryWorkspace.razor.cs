using Microsoft.AspNetCore.Components;
using TheForge.Features.Workspace.Models;
using TheForge.Features.Workspace.Services;
using TheForge.Models;
using TheForge.Services.Interfaces;

namespace TheForge.Features.Workspace;

public partial class QueryWorkspace
{
    [Inject] private IForgeApiService ForgeApi { get; set; } = default!;
    [Inject] private WorkspaceQueryService Queries { get; set; } = default!;
    [Inject] private IThemeService ThemeService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<QueryWorkspace> Logger { get; set; } = default!;

    private readonly WorkspaceState _state = new();
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _queryCancellation;
    private bool _showOfflineModal;
    private bool _disposed;
    private bool _interactive;

    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender) return;
        _interactive = true;
        StateHasChanged();
    }

    protected override async Task OnInitializedAsync()
    {
        ThemeService.ApplyModeTheme(_state.Mode);
        var health = LoadAsync(() => ForgeApi.GetHealthAsync(_lifetime.Token));
        var sources = LoadAsync(() => ForgeApi.GetSourcesAsync(_lifetime.Token));
        var books = LoadAsync(() => ForgeApi.GetBooksAsync(_lifetime.Token));
        await Task.WhenAll(health, sources, books);
        _state.Health = await health;
        _state.SourceList = await sources;
        _state.Books = (await books)?.Books ?? [];
    }

    private async Task<T?> LoadAsync<T>(Func<Task<T?>> load) where T : class
    {
        try { return await load(); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return null; }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Unable to load workspace {PayloadType}", typeof(T).Name);
            return null;
        }
    }

    private void SetMode(WorkspaceMode mode)
    {
        if (_state.IsBusy) return;
        _state.Mode = mode;
        _state.ResetResponse();
        ThemeService.ApplyModeTheme(mode);
    }

    private void SetSubMode(NarratorSubMode subMode)
    {
        if (_state.IsBusy) return;
        _state.SubMode = subMode;
        _state.ResetResponse();
    }

    private async Task SubmitAsync()
    {
        if (_state.SubmitDisabled || _disposed) return;
        if (!_state.IsHealthOnline)
        {
            _showOfflineModal = true;
            return;
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _queryCancellation = cancellation;
        try
        {
            await Queries.ExecuteAsync(_state, RefreshAsync, cancellation.Token);
        }
        finally
        {
            _queryCancellation = null;
        }
    }

    private Task RefreshAsync() => _disposed ? Task.CompletedTask : InvokeAsync(StateHasChanged);
    private void StopQuery() => _queryCancellation?.Cancel();
    private void DismissOfflineDialog() => _showOfflineModal = false;
    private void NavigateToSceneArchive() => Navigation.NavigateTo("/archive");

    public void Dispose()
    {
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
