using Microsoft.Playwright;
using System.Security.Cryptography;

namespace TheForge.Tests;

/// <summary>Opt-in visual checks against a published local build or a deployed site.</summary>
[Explicit("Set THEFORGE_STYLE_CHECK_URL to the published app to inspect. No queries are submitted.")]
public class PublishedStylesTests
{
    [Test]
    public async Task WorkspaceAndArchiveLoadTheirScopedStyles()
    {
        var baseUrl = Environment.GetEnvironmentVariable("THEFORGE_STYLE_CHECK_URL")
                      ?? throw new InvalidOperationException("Set THEFORGE_STYLE_CHECK_URL to the published app.");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
        var failures = new List<string>();
        page.RequestFailed += (_, request) => failures.Add(request.Url + " " + request.Failure);
        await page.GotoAsync(baseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });
        var styleLinks = await page.Locator("link[rel=stylesheet]").EvaluateAllAsync<string[]>("els => els.map(el => el.href)");
        TestContext.Out.WriteLine("Stylesheets: " + string.Join("; ", styleLinks));
        using var http = new HttpClient();
        foreach (var link in styleLinks)
        {
            var response = await http.GetAsync(link);
            Assert.That(response.IsSuccessStatusCode, Is.True, link);
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/css"), link);
            var hash = Convert.ToHexString(SHA256.HashData(await response.Content.ReadAsByteArrayAsync())).ToLowerInvariant();
            Assert.That(new Uri(link).Query, Is.EqualTo("?v=" + hash), "The URL must change when its CSS changes.");
        }
        var statusDisplay = await page.Locator(".status-bar").EvaluateAsync<string>("el => getComputedStyle(el).display");
        var buttonDisplay = await page.Locator(".mode-btn").First.EvaluateAsync<string>("el => getComputedStyle(el).display");
        var font = await page.Locator(".forge-layout").EvaluateAsync<string>("el => getComputedStyle(el).fontFamily");
        TestContext.Out.WriteLine($"Status: {statusDisplay}; mode button: {buttonDisplay}; font: {font}");
        var screenshots = Environment.GetEnvironmentVariable("THEFORGE_STYLE_SCREENSHOTS") ?? TestContext.CurrentContext.TestDirectory;
        Directory.CreateDirectory(screenshots);
        await page.ScreenshotAsync(new() { Path = Path.Combine(screenshots, "published-workspace.png"), FullPage = true });
        Assert.That(statusDisplay, Is.EqualTo("flex"), "The published component CSS is not applying.");
        // A flex item blockifies inline-flex to flex in the computed style.
        Assert.That(buttonDisplay, Is.EqualTo("flex").Or.EqualTo("inline-flex"));
        Assert.That(await page.Locator(".aquila-icon__body path").First.EvaluateAsync<string>("el => getComputedStyle(el).fill"), Is.EqualTo("rgb(212, 175, 55)"));
        await page.GotoAsync(new Uri(new Uri(baseUrl), "/archive").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });
        await page.ScreenshotAsync(new() { Path = Path.Combine(screenshots, "published-archive.png"), FullPage = true });
        Assert.That(await page.Locator(".scene-archive").EvaluateAsync<string>("el => getComputedStyle(el).display"), Is.EqualTo("flex"));
        Assert.That(await page.Locator(".scene-archive").EvaluateAsync<string>("el => getComputedStyle(el).overflowY"), Is.EqualTo("auto"));
        Assert.That(failures.Where(f => f.Contains(".css")), Is.Empty);
        Assert.That(font, Does.Contain("Courier"), "The Explorer font variables must be valid CSS.");
    }
}
