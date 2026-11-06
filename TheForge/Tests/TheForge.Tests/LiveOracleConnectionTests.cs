using Microsoft.Playwright;

namespace TheForge.Tests;

/// <summary>Opt-in checks against an already-running local frontend and real Oracle metadata.</summary>
[Explicit("Requires the local frontend and real Oracle API. Does not submit generation requests.")]
public class LiveOracleConnectionTests
{
    [Test]
    public async Task RendersOracleMetadataAndAcceptsOriginalQuestion()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync("http://127.0.0.1:5157/");
        await page.Locator(".workspace[data-interactive='true']").WaitForAsync(new() { Timeout = 60000 });
        await Assertions.Expect(page.Locator(".status-item--health")).ToHaveTextAsync("ONLINE");
        await Assertions.Expect(page.Locator(".status-item--model")).ToHaveCountAsync(0);
        const string question = "loken saving the girl from jubal when possessed";
        await page.Locator(".query-input").FillAsync(question);
        await Assertions.Expect(page.Locator(".query-input")).ToHaveValueAsync(question);
        await Assertions.Expect(page.Locator(".submit-btn")).ToBeEnabledAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Narrator", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "WIDE NET", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator(".query-input")).ToHaveValueAsync(question);
        await Assertions.Expect(page.Locator(".submit-btn")).ToHaveTextAsync("CHRONICLE");
        await Assertions.Expect(page.Locator(".response-text")).ToHaveCountAsync(0);
        var root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../"));
        var screenshot = Path.Combine(root, "tools/local_frontend/.runtime/live-connected.png");
        Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);
        await page.ScreenshotAsync(new() { Path = screenshot, FullPage = true });
        Assert.That(errors, Is.Empty);
    }
}
