using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using TheForge.Features.SceneArchive.Data;
using Microsoft.Playwright;

namespace TheForge.Tests;

/// <summary>Real Blazor circuits and HTTP transport, with a deterministic local backend.</summary>
[NonParallelizable]
public class WorkspaceBrowserTests
{
    private WebApplication _backend = default!;
    private Process _app = default!;
    private IPlaywright _playwright = default!;
    private IBrowser _browser = default!;
    private string _baseUrl = "";
    private string _root = "";
    private readonly List<string> _appLogs = [];

    [OneTimeSetUp]
    public async Task Start()
    {
        _root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../"));
        var backendUrl = $"http://127.0.0.1:{FreePort()}";
        _baseUrl = $"http://127.0.0.1:{FreePort()}";
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(backendUrl);
        builder.Logging.ClearProviders();
        _backend = builder.Build();
        _backend.MapGet("/health", () => Results.Json(new { status = "ok", ollama_model = "test-model", metadata_loaded = 1234 }));
        _backend.MapGet("/sources", () => Results.Json(new { total = 1, sources = new[] { "Betrayer.pdf" } }));
        _backend.MapGet("/books", () => Results.Json(new { total = 1, books = new[] { new { filename = "Betrayer.pdf", title = "Betrayer", author = "Aaron", category = "novel" } } }));
        foreach (var route in new[] { "/query/focus", "/query/explore" })
        {
            _backend.MapPost(route, async (HttpContext context) =>
            {
                var body = await context.Request.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                if (body.GetProperty("query").GetString() == "slow") await Task.Delay(TimeSpan.FromSeconds(15), context.RequestAborted);
                return Results.Json(new { query = "test", response = "First result\n\nSecond result", sources = new[] { new { source = "Betrayer.pdf", chapter = "9", stitch_range = "1-3", score = 0.9 } }, chunks_used = 3 });
            });
        }
        _backend.MapPost("/query/narrate", async (HttpContext context) =>
        {
            var body = await context.Request.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            context.Response.ContentType = "text/event-stream";
            await context.Response.WriteAsync("data: __STAGE__:generating\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(100, context.RequestAborted);
            await context.Response.WriteAsync("data: Hello\ndata: second line\ndata:\n\ndata:  world\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(1500, context.RequestAborted);
            var query = body.GetProperty("query").GetString();
            if (query == "broken") return;
            if (query == "provider-error")
            {
                await context.Response.WriteAsync("data: [ERROR] quota exhausted\n\ndata: [DONE]\n\n", context.RequestAborted);
                return;
            }
            await context.Response.WriteAsync("data: __SOURCES__:[{\"source\":\"Betrayer.pdf\",\"chapter\":\"9\",\"stitch_range\":\"1-3\",\"score\":0.9}]\n\ndata: [DONE]\n\n", context.RequestAborted);
        });
        await _backend.StartAsync();

        var config = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        // Use the configuration built by dotnet test (Debug or Release).
        config.ArgumentList.Add(Path.Combine(TestContext.CurrentContext.TestDirectory, "TheForge.dll"));
        config.Environment["ASPNETCORE_URLS"] = _baseUrl;
        config.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        config.Environment["FastApi__BaseUrl"] = backendUrl;
        config.Environment["Logging__EventLog__LogLevel__Default"] = "None";
        var archivePath = Path.Combine(TestContext.CurrentContext.TestDirectory, $"archive-{Guid.NewGuid():N}.db");
        await using (var archive = new SceneArchiveDbContext(new DbContextOptionsBuilder<SceneArchiveDbContext>()
            .UseSqlite($"Data Source={archivePath}").Options))
        {
            await archive.Database.EnsureCreatedAsync();
        }
        config.Environment["ConnectionStrings__SceneArchiveDb"] = $"Data Source={archivePath};Mode=ReadOnly";
        _app = Process.Start(config)!;
        _app.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (_appLogs) _appLogs.Add(e.Data); };
        _app.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (_appLogs) _appLogs.Add(e.Data); };
        _app.BeginOutputReadLine();
        _app.BeginErrorReadLine();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var ready = false;
        for (var i = 0; i < 60 && !_app.HasExited; i++)
        {
            try { ready = (await http.GetAsync(_baseUrl + "/api/health")).IsSuccessStatusCode; }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            if (ready) break;
            await Task.Delay(150);
        }
        Assert.That(ready, Is.True, string.Join(Environment.NewLine, _appLogs));
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    [Test]
    public async Task ExplorerFocusWideNetFiltersAndResponsiveStylesWork()
    {
        var page = await _browser.NewPageAsync(new() { ViewportSize = new() { Width = 1365, Height = 900 } });
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync(_baseUrl);
        await page.Locator(".workspace[data-interactive='true']").WaitForAsync();
        await Assertions.Expect(page.Locator(".workspace")).ToHaveAttributeAsync("data-interactive", "true");
        await Assertions.Expect(page.Locator(".status-item--health")).ToHaveTextAsync("ONLINE");
        await page.Locator(".query-input").FillAsync("test");
        await page.Locator(".submit-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".explorer-section")).ToHaveCountAsync(2);
        await Assertions.Expect(page.Locator(".source-card")).ToHaveCountAsync(1);

        await page.GetByRole(AriaRole.Button, new() { Name = "Narrator", Exact = true }).ClickAsync();
        await page.Locator(".filter-input").FillAsync("Betrayer");
        await page.Locator(".book-typeahead__result").ClickAsync();
        await Assertions.Expect(page.Locator(".active-book__title")).ToHaveTextAsync("Betrayer");
        await page.Locator(".submit-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".complete-mark")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".response-text")).ToContainTextAsync("First result");
        await page.GetByRole(AriaRole.Button, new() { Name = "WIDE NET", Exact = true }).ClickAsync();
        await page.Locator(".submit-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".response-text")).ToContainTextAsync("Hello\nsecond line\n world");
        await Assertions.Expect(page.Locator(".stop-btn")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".complete-mark")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".source-card")).ToContainTextAsync("Chunks 1-3");
        Assert.That(await page.Locator(".query-panel").EvaluateAsync<string>("el => getComputedStyle(el).display"), Is.EqualTo("grid"));
        Assert.That(await page.Locator(".source-list").EvaluateAsync<string>("el => getComputedStyle(el).display"), Is.EqualTo("grid"));

        await page.SetViewportSizeAsync(390, 844);
        await Assertions.Expect(page.Locator(".status-item--model")).ToHaveCountAsync(0);
        Assert.That(await page.Locator(".brand-panel").EvaluateAsync<string>("el => getComputedStyle(el).flexDirection"), Is.EqualTo("column"));
        await page.ScreenshotAsync(new() { Path = Path.Combine(TestContext.CurrentContext.TestDirectory, "workspace-mobile.png"), FullPage = true });
        Assert.That(errors, Is.Empty);
        await page.CloseAsync();
    }

    [TestCase("Explorer")]
    [TestCase("Narrator")]
    public async Task StopCancelsSynchronousQueriesInTheBrowser(string mode)
    {
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_baseUrl);
        await page.Locator(".workspace[data-interactive='true']").WaitForAsync();
        await Assertions.Expect(page.Locator(".workspace")).ToHaveAttributeAsync("data-interactive", "true");
        await Assertions.Expect(page.Locator(".status-item--health")).ToHaveTextAsync("ONLINE");
        await page.GetByRole(AriaRole.Button, new() { Name = mode, Exact = true }).ClickAsync();
        await page.Locator(".query-input").FillAsync("slow");
        await page.Locator(".submit-btn").ClickAsync();
        await page.Locator(".stop-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".interrupted")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".submit-btn")).ToBeEnabledAsync();
        await page.CloseAsync();
    }

    [Test]
    [TestCase("broken")]
    [TestCase("provider-error")]
    public async Task FailedStreamsStayFailedInTheBrowser(string query)
    {
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_baseUrl);
        await page.Locator(".workspace[data-interactive='true']").WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Narrator", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "WIDE NET", Exact = true }).ClickAsync();
        await page.Locator(".query-input").FillAsync(query);
        await page.Locator(".submit-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".response-error")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".complete-mark")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator(".source-card")).ToHaveCountAsync(0);
        await page.CloseAsync();
    }

    [Test]
    public async Task FeatureRoutesAndCollocatedJavascriptAreAvailable()
    {
        using var http = new HttpClient();
        foreach (var path in new[] { "/archive", "/settings", "/query", "/Features/SceneArchive/Components/SceneOverlay.razor.js", "/TheForge.styles.css" })
            Assert.That((await http.GetAsync(_baseUrl + path)).StatusCode, Is.EqualTo(HttpStatusCode.OK), path);
        var settings = await http.GetStringAsync(_baseUrl + "/settings");
        Assert.That(settings, Does.Contain("href=\"/\""));
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
        if (_app is not null)
        {
            if (!_app.HasExited) { _app.Kill(entireProcessTree: true); await _app.WaitForExitAsync(); }
            lock (_appLogs) File.WriteAllLines(Path.Combine(TestContext.CurrentContext.TestDirectory, "app.log"), _appLogs);
            _app.Dispose();
        }
        if (_backend is not null) await _backend.DisposeAsync();
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
