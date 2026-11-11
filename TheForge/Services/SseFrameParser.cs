using System.Runtime.CompilerServices;
using System.Text;

namespace TheForge.Services;

/// <summary>
/// Reads complete Server-Sent Event data payloads. A single event may contain
/// multiple "data:" lines; EOF without a terminal marker is NOT success.
/// No dependency on the Python backend or Blazor.
/// </summary>
public static class SseFrameParser
{
    public static async IAsyncEnumerable<string> ReadAsync(
        TextReader reader,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var fields = new List<string>();

        while (true)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                throw new IOException(
                    "The upstream stream closed without [DONE] or [ERROR].");
            }

            if (line.Length == 0)
            {
                if (fields.Count == 0)
                {
                    continue;
                }

                var data = string.Join("\n", fields);
                fields.Clear();
                yield return data;

                if (data == "[DONE]" ||
                    data.StartsWith("[ERROR]", StringComparison.Ordinal))
                {
                    yield break;
                }

                continue;
            }

            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                // SSE comments, "event:" and other fields are not content.
                continue;
            }

            var value = line["data:".Length..];
            if (value.StartsWith(' '))
            {
                value = value[1..];
            }

            fields.Add(value);
        }
    }

    public static string EncodeData(string payload)
    {
        var normalized = payload.Replace("\r\n", "\n").Replace('\r', '\n');
        var builder = new StringBuilder();
        foreach (var line in normalized.Split('\n'))
        {
            builder.Append("data: ").Append(line).Append("\n");
        }
        return builder.Append('\n').ToString();
    }
}
