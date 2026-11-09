using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TheForge.Models;
using TheForge.Services;

namespace TheForge.Tests;

public class FastApiClientTests
{
    [Test]
    public async Task LegacyDefaultUsesExistingExplorerRoute()
    {
        var handler = new Backend("{\"response\":\"analysis\"}", "application/json");
        var result = await Client(handler).QueryAsync(new() { Query = "ordinary question" });
        Assert.That(handler.Path, Is.EqualTo("/query/explore"));
        Assert.That(System.Text.Json.JsonDocument.Parse(handler.Body!).RootElement.GetProperty("query").GetString(), Is.EqualTo("ordinary question"));
        Assert.That(result.Response, Is.EqualTo("analysis"));
    }

    [Test]
    public async Task NarrationAccumulatesSseTextAndSourcesRatherThanParsingJson()
    {
        var handler = new Backend("data: __STAGE__:generating\r\n\r\ndata: First\r\ndata: second\r\ndata:\r\n\r\ndata:  next\n\ndata: __SOURCES__:[{\"source\":\"book\",\"stitch_range\":\"1-3\"}]\n\ndata: [DONE]\n\n");
        var result = await Client(handler).QueryNarrateAsync(new() { Query = "question" });
        Assert.That(handler.Path, Is.EqualTo("/query/narrate"));
        Assert.That(result.Response, Is.EqualTo("First\nsecond\n next"));
        Assert.That(result.Sources.Single().StitchRange, Is.EqualTo("1-3"));
    }

    [Test]
    public async Task LegacyStreamUsesNarrationAndPreservesEventBoundaries()
    {
        var handler = new Backend("data:first\ndata: second\n\ndata: [DONE]\n\n");
        var events = new List<string>();
        await foreach (var data in Client(handler).QueryStreamAsync(new() { Query = "question" })) events.Add(data);
        Assert.That(handler.Path, Is.EqualTo("/query/narrate"));
        Assert.That(events, Is.EqualTo(new[] { "first\nsecond" }));
    }

    [Test]
    public void MissingCompletionCannotProduceASuccessfulNarrative()
    {
        Assert.ThrowsAsync<IOException>(() => Client(new Backend("data: partial\n\n"))
            .QueryNarrateAsync(new() { Query = "question" }));
    }

    [Test]
    public void ProviderErrorCannotProduceASuccessfulNarrative()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() => Client(new Backend("data: partial\n\ndata: [ERROR] quota exhausted\n\ndata: [DONE]\n\n"))
            .QueryNarrateAsync(new() { Query = "question" }));
    }

    [Test]
    public async Task GatewayEncodingRoundTripsMultilineWhitespace()
    {
        const string text = " first\n\nlast \n";
        var events = new List<string>();
        await foreach (var data in ServerSentEvents.ReadAsync(new StringReader(ServerSentEvents.Encode(text)))) events.Add(data);
        Assert.That(events.Single(), Is.EqualTo(text));
    }

    [Test]
    public async Task SessionIdentifierIsForwardedInSnakeCase()
    {
        var backend = new Backend("{\\"response\\":\\"analysis\\"}", "application/json");
        await Client(backend).QueryAsync(new() {
            Query = "ordinary question",
            SessionId = "forge-session-123"
        });
        using var payload = System.Text.Json.JsonDocument.Parse(backend.Body!);
        Assert.That(payload.RootElement.GetProperty("session_id").GetString(),
            Is.EqualTo("forge-session-123"));
    }

    private static FastApiClient Client(Backend backend) => new(
        new HttpClient(backend) { BaseAddress = new("http://oracle/") }, NullLogger<FastApiClient>.Instance);

    private sealed class Backend(string body, string contentType = "text/event-stream") : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Path = request.RequestUri!.AbsolutePath;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, contentType) };
        }
    }
}
