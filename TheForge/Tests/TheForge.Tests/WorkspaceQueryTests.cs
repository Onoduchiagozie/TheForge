using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TheForge.Features.Workspace.Models;
using TheForge.Features.Workspace.Services;
using TheForge.Models;
using TheForge.Services.Interfaces;

namespace TheForge.Tests;

public class WorkspaceQueryTests
{
    [TestCase(WorkspaceMode.Explorer, NarratorSubMode.Focus, "explore")]
    [TestCase(WorkspaceMode.Narrator, NarratorSubMode.Focus, "focus")]
    [TestCase(WorkspaceMode.Narrator, NarratorSubMode.WideNet, "narrate")]
    public async Task RoutesModesAndAppliesResults(WorkspaceMode mode, NarratorSubMode subMode, string route)
    {
        var state = new WorkspaceState { Mode = mode, SubMode = subMode, QueryText = "  question  " };
        state.SelectBook("betrayer.pdf");
        var api = new FakeApi();
        var updates = 0;
        await Service(api).ExecuteAsync(state, () => { updates++; return Task.CompletedTask; });
        Assert.That(api.Route, Is.EqualTo(route));
        Assert.That(api.Request!.Query, Is.EqualTo("question"));
        Assert.That(api.Request.BookFilter, Is.EqualTo(mode == WorkspaceMode.Narrator ? "betrayer.pdf" : null));
        Assert.That(state.Status, Is.EqualTo(StreamState.Complete));
        Assert.That(state.ResponseText, Is.EqualTo(mode == WorkspaceMode.Narrator && subMode == NarratorSubMode.WideNet ? "Hello world" : "First\n\nSecond"));
        Assert.That(state.Sources, Has.Count.EqualTo(1));
        Assert.That(updates, Is.GreaterThanOrEqualTo(2));
        if (mode == WorkspaceMode.Explorer) Assert.That(state.ExplorerSections, Has.Count.EqualTo(2));
        if (state.IsWideNet) Assert.That(state.Sources[0].StitchRange, Is.EqualTo("1-3"));
    }

    [TestCase(WorkspaceMode.Explorer, NarratorSubMode.Focus)]
    [TestCase(WorkspaceMode.Narrator, NarratorSubMode.Focus)]
    [TestCase(WorkspaceMode.Narrator, NarratorSubMode.WideNet)]
    public async Task StopCancelsEveryQueryPath(WorkspaceMode mode, NarratorSubMode subMode)
    {
        using var cancellation = new CancellationTokenSource();
        var api = new FakeApi { WaitForCancellation = true };
        var state = new WorkspaceState { Mode = mode, SubMode = subMode, QueryText = "question" };
        var execution = Service(api).ExecuteAsync(state, () => Task.CompletedTask, cancellation.Token);
        await api.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancellation.Cancel();
        await execution.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(state.Status, Is.EqualTo(StreamState.Cancelled));
        Assert.That(state.ErrorMessage, Is.Null);
    }

    [Test]
    public async Task BackendFailureIsLoggedAndPresentedAsAnError()
    {
        var state = new WorkspaceState { QueryText = "question" };
        await Service(new FakeApi { Failure = new HttpRequestException("private backend details") })
            .ExecuteAsync(state, () => Task.CompletedTask);
        Assert.That(state.Status, Is.EqualTo(StreamState.Error));
        Assert.That(state.ErrorMessage, Is.EqualTo("Explorer analysis failed."));
        Assert.That(state.ErrorMessage, Does.Not.Contain("private"));
    }

    [Test]
    public async Task StreamErrorKeepsPartialTextAndDoesNotBecomeComplete()
    {
        var state = new WorkspaceState { Mode = WorkspaceMode.Narrator, SubMode = NarratorSubMode.WideNet, QueryText = "question" };
        var api = new FakeApi { Stream = "data: Partial\n\ndata: [ERROR] generation failed\n\ndata: [DONE]\n\n" };
        await Service(api).ExecuteAsync(state, () => Task.CompletedTask);
        Assert.That(state.ResponseText, Is.EqualTo("Partial"));
        Assert.That(state.Status, Is.EqualTo(StreamState.Error));
    }

    [Test]
    public void FiltersAreExclusiveAndPromptSelectionSynchronizesTheBook()
    {
        var state = new WorkspaceState { Mode = WorkspaceMode.Narrator, Books = [new() { Title = "Betrayer", Filename = "betrayer.pdf" }] };
        state.SelectSources(["source-a"]);
        state.SelectChip(new("Angron", "Betrayer"));
        Assert.That(state.SelectedBookFilename, Is.EqualTo("betrayer.pdf"));
        Assert.That(state.SelectedSources, Is.Empty);
        state.SelectSources(["source-b"]);
        Assert.That(state.SelectedBookFilename, Is.Null);
        Assert.That(state.BookClearSignal, Is.Not.Null);
        state.SelectChip(new("Angron", "Betrayer"));
        state.SelectChip(new("No book"));
        Assert.That(state.SelectedBookFilename, Is.Null);
    }

    [Test]
    public async Task BusyWorkspaceDoesNotStartAnotherRequest()
    {
        var state = new WorkspaceState { QueryText = "question", Status = StreamState.Streaming };
        var api = new FakeApi();
        await Service(api).ExecuteAsync(state, () => Task.CompletedTask);
        Assert.That(api.Route, Is.Null);
    }

    private static WorkspaceQueryService Service(FakeApi api) => new(api,
        new QueryStreamParser(NullLogger<QueryStreamParser>.Instance), NullLogger<WorkspaceQueryService>.Instance);

    [TestCase(WorkspaceMode.Explorer)]
    [TestCase(WorkspaceMode.Narrator)]
    public async Task ProviderDetailsNeverBecomeACompletedAnswer(WorkspaceMode mode)
    {
        var state = new WorkspaceState { Mode = mode, QueryText = "question" };
        await Service(new FakeApi { Answer = "[ERROR] Gemini models exhausted: HTTP 429 from private-model" })
            .ExecuteAsync(state, () => Task.CompletedTask);
        Assert.That(state.Status, Is.EqualTo(StreamState.Error));
        Assert.That(state.ResponseText, Is.Empty);
        Assert.That(state.Sources, Is.Empty);
        Assert.That(state.ErrorMessage, Is.EqualTo("The service is temporarily busy. Please try again later."));
    }

    private sealed class FakeApi : IForgeApiService
    {
        public string? Route { get; private set; }
        public QueryRequest? Request { get; private set; }
        public bool WaitForCancellation { get; init; }
        public Exception? Failure { get; init; }
        public string Answer { get; init; } = "First\n\nSecond";
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Stream { get; init; } = "data: __STAGE__:generating\n\ndata: Hello\n\ndata:  world\n\ndata: __SOURCES__:[{\"source\":\"Betrayer\",\"stitch_range\":\"1-3\"}]\n\ndata: [DONE]\n\n";
        public Task<HealthPayload?> GetHealthAsync(CancellationToken ct = default) => Task.FromResult<HealthPayload?>(new() { Status = "ok" });
        public Task<SourceListPayload?> GetSourcesAsync(CancellationToken ct = default) => Task.FromResult<SourceListPayload?>(new());
        public Task<BookListPayload?> GetBooksAsync(CancellationToken ct = default) => Task.FromResult<BookListPayload?>(new());
        public Task<QueryResponse?> QueryExploreAsync(QueryRequest request, CancellationToken ct = default) => Query("explore", request, ct);
        public Task<QueryResponse?> QueryFocusAsync(QueryRequest request, CancellationToken ct = default) => Query("focus", request, ct);
        public async Task<HttpResponseMessage> BeginNarrateStreamAsync(QueryRequest request, CancellationToken ct = default)
        {
            await Begin("narrate", request, ct);
            return new(HttpStatusCode.OK) { Content = new StringContent(Stream) };
        }
        private async Task<QueryResponse?> Query(string route, QueryRequest request, CancellationToken ct)
        {
            await Begin(route, request, ct);
            return new() { Response = Answer, Sources = [new() { Source = "Betrayer" }] };
        }
        private async Task Begin(string route, QueryRequest request, CancellationToken ct)
        {
            Route = route;
            Request = request;
            Started.TrySetResult();
            if (WaitForCancellation) await Task.Delay(Timeout.Infinite, ct);
            if (Failure is not null) throw Failure;
        }
    }
}
