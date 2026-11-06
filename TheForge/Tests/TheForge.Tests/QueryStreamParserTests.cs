using Microsoft.Extensions.Logging.Abstractions;
using TheForge.Features.Workspace.Models;
using TheForge.Features.Workspace.Services;

namespace TheForge.Tests;

public class QueryStreamParserTests
{
    private readonly QueryStreamParser _parser = new(NullLogger<QueryStreamParser>.Instance);

    [Test]
    public async Task PreservesTokenWhitespaceAndCombinesMultilineEvents()
    {
        var frames = await Parse(": heartbeat\n\ndata:  first \ndata: second\n\ndata:\n\ndata: [DONE]\n\ndata: ignored\n\n");
        Assert.That(frames.Select(f => f.Text), Is.EqualTo(new[] { " first \nsecond", "", "" }));
        Assert.That(frames.Last().Kind, Is.EqualTo(QueryStreamFrameKind.Complete));
    }

    [Test]
    public async Task ParsesStagesAndSnakeCaseSourceMetadata()
    {
        var frames = await Parse("data: __STAGE__:reranking\n\ndata: __SOURCES__:[{\"source\":\"Betrayer.pdf\",\"chapter\":\"9\",\"stitch_range\":\"10-12\",\"score\":0.9}]\n\ndata: [DONE]\n\ndata: [DONE]\n\n");
        Assert.That(frames, Has.Count.EqualTo(3));
        Assert.That(frames[0].Stage, Is.EqualTo(QueryStage.Reranking));
        Assert.That(frames[1].Sources![0].StitchRange, Is.EqualTo("10-12"));
    }

    [Test]
    public async Task MalformedSourcesDoNotLoseFollowingTextOrFinalUnterminatedEvent()
    {
        var frames = await Parse("data: __SOURCES__:not-json\n\ndata: still here");
        Assert.That(frames, Has.Count.EqualTo(2));
        Assert.That(frames[0].Text, Is.EqualTo("still here"));
        Assert.That(frames[1].Kind, Is.EqualTo(QueryStreamFrameKind.Error));
    }

    [Test]
    public async Task ErrorEndsTheStream()
    {
        var frames = await Parse("data:[ERROR] backend offline\n\ndata: ignored\n\n");
        Assert.That(frames.Single().Kind, Is.EqualTo(QueryStreamFrameKind.Error));
        Assert.That(frames.Single().Text, Is.EqualTo("backend offline"));
    }

    [Test]
    public void CancellationIsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () =>
        {
            await foreach (var frame in _parser.ReadAsync(new StringReader("data: hello\n\n"), cancellation.Token)) { }
        });
    }

    private async Task<List<QueryStreamFrame>> Parse(string input)
    {
        var frames = new List<QueryStreamFrame>();
        await foreach (var frame in _parser.ReadAsync(new StringReader(input))) frames.Add(frame);
        return frames;
    }
}
