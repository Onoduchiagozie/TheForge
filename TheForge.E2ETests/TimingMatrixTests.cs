using System.Diagnostics;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace TheForge.E2ETests;

[TestFixture]
public class TimingMatrixTests : PageTest
{
    private const string BaseUrl = "http://192.168.50.100:5157";
    private const string NarratorQuery = "Leman Russ fights Magnus on prospero";
    private const string ExplorerQuery = "why did horus turn traitor";
    private const string BookFilterSearchTerm = "Galaxy in Flames";

    // Model is fixed at google/gemma-4-e4b for this whole run — set in
    // LM Studio before starting, not something this test can control.
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromMinutes(15);

    private readonly List<(string Run, bool Success, double Seconds, string Note)> _results = new();

    [Test]
    public async Task RunFullTimingMatrix()
    {
        await Goto();
        await RunCase("Focus - no filter", async () =>
        {
            await SetMode("Narrator");
            await SetSubMode("FOCUS");
            await SubmitQuery(NarratorQuery);
        });

        await Goto();
        await RunCase("Focus - book filter (Galaxy in Flames)", async () =>
        {
            await SetMode("Narrator");
            await SetSubMode("FOCUS");
            await SelectBookFilter(BookFilterSearchTerm);
            await SubmitQuery(NarratorQuery);
        });

        await Goto();
        await RunCase("Wide Net - no filter", async () =>
        {
            await SetMode("Narrator");
            await SetSubMode("WIDE NET");
            await SubmitQuery(NarratorQuery);
        });

        await Goto();
        await RunCase("Wide Net - book filter (Galaxy in Flames)", async () =>
        {
            await SetMode("Narrator");
            await SetSubMode("WIDE NET");
            await SelectBookFilter(BookFilterSearchTerm);
            await SubmitQuery(NarratorQuery);
        });

        await Goto();
        await RunCase("Explorer - no filter available", async () =>
        {
            await SetMode("Explorer");
            await SubmitQuery(ExplorerQuery);
        });

        PrintResultsTable();

        // Fail the overall test if anything genuinely errored (not just slow),
        // so a red run is impossible to miss in CI/console output.
        var failures = _results.Where(r => !r.Success).ToList();
        Assert.That(failures, Is.Empty,
            $"{failures.Count} run(s) failed: {string.Join("; ", failures.Select(f => f.Run))}");
    }

    private async Task Goto()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForSelectorAsync(".workspace");
    }

    private async Task RunCase(string label, Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        string note = "";
        bool success;

        try
        {
            await action();
            sw.Stop();

            // Correctness check: did we get real content, or the empty/error state?
            var errorVisible = await Page.Locator(".response-error").IsVisibleAsync();
            var emptyVisible = await Page.Locator(".empty-state").IsVisibleAsync();

            if (errorVisible)
            {
                var errText = await Page.Locator(".response-error").InnerTextAsync();
                note = $"ERROR shown: {errText}";
                success = false;
            }
            else if (emptyVisible)
            {
                note = "Empty result (no records found)";
                success = false;
            }
            else
            {
                var responseText = await Page.Locator(".response-text").First.InnerTextAsync();
                var sourceCount = await Page.Locator(".source-card").CountAsync();
                note = $"{responseText.Length} chars, {sourceCount} sources";
                success = responseText.Trim().Length > 0;
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            note = $"EXCEPTION: {ex.Message}";
            success = false;
        }

        _results.Add((label, success, sw.Elapsed.TotalSeconds, note));
    }

    private async Task SetMode(string modeName)
    {
        await Page.ClickAsync($".mode-btn:has-text(\"{modeName}\")");
        // Give the mode-switch reset (ResetResponseState) a moment to settle.
        await Task.Delay(200);
    }

    private async Task SetSubMode(string subModeName)
    {
        await Page.ClickAsync($".submode-toggle__btn:has-text(\"{subModeName}\")");
        await Task.Delay(200);
    }

    private async Task SelectBookFilter(string searchTerm)
    {
        await Page.FillAsync(".filter-input", searchTerm);
        await Page.WaitForSelectorAsync(".book-typeahead__result", new() { Timeout = 10000 });
        await Page.ClickAsync(".book-typeahead__result >> nth=0");
        // Confirm the clear button appeared — proves a selection actually landed,
        // not just that text sits in the input.
        await Expect(Page.Locator(".book-typeahead__clear")).ToBeVisibleAsync();
    }

    private async Task SubmitQuery(string query)
    {
        await Page.FillAsync(".query-input", query);
        await Page.ClickAsync(".submit-btn");

        // Completion looks different per path:
        //  - Focus/Explorer (sync): submit-btn re-enables once StreamState
        //    leaves Streaming (Complete/Error).
        //  - Wide Net (SSE): .complete-mark appears at the end of the stream,
        //    OR .response-error / .empty-state on failure.
        await Page.WaitForFunctionAsync(
            @"() => {
                const err = document.querySelector('.response-error');
                const empty = document.querySelector('.empty-state');
                const complete = document.querySelector('.complete-mark');
                const submitBtn = document.querySelector('.submit-btn');
                const stillStreaming = submitBtn && submitBtn.textContent.includes('…');
                return !!err || !!empty || !!complete || !stillStreaming;
            }",
            new PageWaitForFunctionOptions { Timeout = (float)CompletionTimeout.TotalMilliseconds });
    }

    private void PrintResultsTable()
    {
        TestContext.Out.WriteLine();
        TestContext.Out.WriteLine("=== TIMING MATRIX — google/gemma-4-e4b ===");
        TestContext.Out.WriteLine($"{"Run",-45} {"Result",-8} {"Time (s)",-10} Notes");
        TestContext.Out.WriteLine(new string('-', 100));
        foreach (var (run, success, seconds, note) in _results)
        {
            TestContext.Out.WriteLine(
                $"{run,-45} {(success ? "PASS" : "FAIL"),-8} {seconds,-10:F1} {note}");
        }
        TestContext.Out.WriteLine();
    }
}