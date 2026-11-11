using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TheForge.Controllers;
using TheForge.Models;
using TheForge.Services;

var passed = 0;

void Check(bool condition, string message)
{
    if (!condition)
        throw new Exception("FAIL: " + message);
}

async Task Verify(string name, string upstreamSse, Func<string, bool> predicate)
{
    var handler = new FixedSseHandler(upstreamSse);
    using var http = new HttpClient(handler)
    {
        BaseAddress = new Uri("http://localhost:8000/")
    };
    var fastApi = new FastApiClient(http, NullLogger<FastApiClient>.Instance);
    using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 20 });
    var cache = new QueryCacheService(memoryCache, NullLogger<QueryCacheService>.Instance);
    var controller = new QueryController(
        fastApi, cache, NullLogger<QueryController>.Instance);
    var ctx = new DefaultHttpContext();
    var body = new MemoryStream();
    ctx.Response.Body = body;
    controller.ControllerContext = new ControllerContext { HttpContext = ctx };

    await controller.NarrateStream(new QueryRequest { Query = "Describe the Siege of Terra" },
        CancellationToken.None);

    var downstream = Encoding.UTF8.GetString(body.ToArray());
    Check(predicate(downstream), name + " response did not match contract: " + downstream);
    Console.WriteLine("PASS: " + name);
    passed++;
}

var complete =
    "data: __STAGE__:generating\n\n" +
    "data: The fortress burned.\ndata: The defenders held.\n\n" +
    "data: __SOURCES__:[{\"source\":\"siege.pdf\"}]\n\n" +
    "data: __STAGE__:complete\n\n" +
    "data: [DONE]\n\n";

await Verify("complete answer with multiple SSE data lines", complete,
    x => x.Contains("data: The fortress burned.\ndata: The defenders held.\n\n")
      && x.Contains("data: __SOURCES__:")
      && x.Contains("data: [DONE]\n\n")
      && !x.Contains("[ERROR]"));

await Verify("provider failure is error, never DONE",
    "data: __STAGE__:generating\n\n" +
    "data: [ERROR] Gemini HTTP 503\n\n",
    x => x.Contains("[ERROR] Gemini HTTP 503") && !x.Contains("[DONE]"));

await Verify("sources but zero generation is error",
    "data: __SOURCES__:[{\"source\":\"siege.pdf\"}]\n\n" +
    "data: [DONE]\n\n",
    x => x.Contains("[ERROR] Retrieval finished but the language model returned no answer.")
      && !x.Contains("[DONE]"));

await Verify("truncated connection is error, never DONE",
    "data: Some answer text\n\n" +
    "data: __SOURCES__:[{\"source\":\"siege.pdf\"}]\n\n",
    x => x.Contains("[ERROR] The upstream response failed or disconnected.")
      && !x.Contains("[DONE]"));

await Verify("backend failed stage is error",
    "data: Some answer text\n\n" +
    "data: __STAGE__:failed\n\n" +
    "data: [DONE]\n\n",
    x => x.Contains("[ERROR] The upstream generation failed.") && !x.Contains("[DONE]"));

await Verify("comments and event fields ignored",
    ": a keepalive\nevent: message\ndata: Recovered answer\n\n" +
    "data: [DONE]\n\n",
    x => x.Contains("data: Recovered answer\n\n")
      && !x.Contains("data: : a keepalive")
      && x.Contains("[DONE]"));

await Verify("incomplete final event is not trusted",
    "data: almost finished\n\ndata: [DONE]",
    x => x.Contains("[ERROR] The upstream response failed or disconnected.")
      && !x.Contains("data: [DONE]\n\n"));

Console.WriteLine($"STREAM CONTRACT TESTS: {passed}/7 PASSED");

internal sealed class FixedSseHandler(string responseSse) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseSse, Encoding.UTF8, "text/event-stream")
        });
    }
}
