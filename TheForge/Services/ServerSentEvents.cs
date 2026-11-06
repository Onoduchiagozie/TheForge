using System.Runtime.CompilerServices;

namespace TheForge.Services;

/// <summary>Preserves complete SSE data events, including whitespace and embedded newlines.</summary>
public static class ServerSentEvents
{
    public static async IAsyncEnumerable<string> ReadAsync(TextReader reader,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var data = new List<string>();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line is null || line.Length == 0)
            {
                if (data.Count > 0)
                {
                    yield return string.Join("\n", data);
                    data.Clear();
                }
                if (line is null) yield break;
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var value = line[5..];
                data.Add(value.StartsWith(' ') ? value[1..] : value);
            }
        }
    }

    public static string Encode(string data) =>
        "data: " + data.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\ndata: ") + "\n\n";
}
