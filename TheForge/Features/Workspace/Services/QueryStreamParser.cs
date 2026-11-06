using System.Runtime.CompilerServices;
using System.Text.Json;
using TheForge.Features.Workspace.Models;
using TheForge.Models;
using TheForge.Services;

namespace TheForge.Features.Workspace.Services;

/// <summary>Translates SSE events into workspace events without depending on UI rendering.</summary>
public sealed class QueryStreamParser(ILogger<QueryStreamParser> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public async IAsyncEnumerable<QueryStreamFrame> ReadAsync(TextReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var data in ServerSentEvents.ReadAsync(reader, cancellationToken))
        {
            var frame = Parse(data);
            if (frame is not null)
            {
                yield return frame;
                if (frame.Kind is QueryStreamFrameKind.Complete or QueryStreamFrameKind.Error) yield break;
            }
        }
        yield return new(QueryStreamFrameKind.Error, "Oracle stream ended before completion.");
    }

    private QueryStreamFrame? Parse(string data)
    {
        var control = data.Trim();
        if (control == "[DONE]") return new(QueryStreamFrameKind.Complete);
        if (control.StartsWith("[ERROR]", StringComparison.Ordinal)) return new(QueryStreamFrameKind.Error, control[7..].Trim());
        if (control.StartsWith("__STAGE__:", StringComparison.Ordinal)) return new(QueryStreamFrameKind.Stage, Stage: QueryStageExtensions.Parse(control[10..]));
        if (!control.StartsWith("__SOURCES__:", StringComparison.Ordinal)) return new(QueryStreamFrameKind.Text, data);
        try
        {
            return new(QueryStreamFrameKind.Sources, Sources: JsonSerializer.Deserialize<List<SourceInfo>>(control[12..], JsonOptions) ?? []);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Malformed source frame ignored.");
            return null;
        }
    }
}
