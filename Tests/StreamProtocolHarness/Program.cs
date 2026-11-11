using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TheForge.Models;
using TheForge.Services;

static class AssertEx
{
    public static void True(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Console.WriteLine("PASS " + label);
    }
}

internal sealed class FakeHandler(string payload) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "text/event-stream")
        });
}

class Runner
{
    static async Task<List<string>> Frames(string body)
    {
        var client = new FastApiClient(
            new HttpClient(new FakeHandler(body)) { BaseAddress = new Uri("http://localhost/") },
            NullLogger<FastApiClient>.Instance);
        var received = new List<string>();
        await foreach (var frame in client.QueryNarrateStreamAsync(
            new QueryRequest { Query = "Narrate the Burning of Prospero" }))
            received.Add(frame);
        return received;
    }

    public static async Task Main()
    {
        var completed = await Frames(
            "data: __STAGE__:retrieving\n\n" +
            "data: __STAGE__:generating\n\n" +
            "data: Magnus watched the sky burn.\n\n" +
            "data: __SOURCES__:[]\n\n" +
            "data: __STAGE__:complete\n\n" +
            "data: [DONE]\n\n");
        AssertEx.True(completed.Contains("Magnus watched the sky burn."),
            "A successful non-Horus narration yields answer text");
        AssertEx.True(completed.Last() == "[DONE]", "Success propagates real [DONE]");
        AssertEx.True(completed.Any(x => x.StartsWith("__SOURCES__:")),
            "Sources are retained");
        AssertEx.True(completed.Count(x => x == "[DONE]") == 1,
            "Exactly one completion signal");

        var interrupted = await Frames(
            "data: A passage about the Night Lords.\n\n" +
            "data: __SOURCES__:[]\n\n"
        AssertEx.True(interrupted.Last().StartsWith("[ERROR]"),
            "Premature EOF after receiving sources reports error, not success");
        AssertEx.True(!interrupted.Contains("[DONE]"),
            "Premature EOF cannot claim success");

        var failed = await Frames(
            "data: __STAGE__:generating\n\n" +
            "data: __STAGE__:failed\n\n" +
            "data: [DONE]\n\n");
        AssertEx.True(failed.Last().StartsWith("[ERROR]"),
            "Failed upstream stage is a terminal error");
        AssertEx.True(!failed.Contains("[DONE]"),
            "A fake [DONE] after failure is not accepted");

        var explicitError = await Frames(
            "data: [ERROR] Provider refused the request\n\n" +
            "data: [DONE]\n\n");
        AssertEx.True(explicitError.Count == 1 &&
                      explicitError[0].StartsWith("[ERROR]"),
            "Provider error is not converted into success");

        var multiline = await Frames(
            "data: Russ stepped forward.\n" +
            "data: Magnus raised his staff.\n\n" +
            "data: [DONE]\n\n");
        AssertEx.True(multiline[0] == "Russ stepped forward.\nMagnus raised his staff.",
            "Multiline SSE frame preserves paragraph breaks");

        var noAnswer = await Frames("data: __SOURCES__:[]\n\ndata: [DONE]\n\n");
        AssertEx.True(noAnswer.Contains("[DONE]"),
            "Gateway transmits completion; frontend is responsible for rejecting empty answer");

        Console.WriteLine("PASS: 6 controlled SSE scenarios, no real Oracle or Gemini.");
    }
}
