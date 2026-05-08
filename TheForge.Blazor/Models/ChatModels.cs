namespace TheForge.Blazor.Models;

public enum ChatRole { User, Forge, Error }
public enum ChatMode  { Remembrancer, Narrator, Explorer }

public class ChatMessage
{
    public ChatRole Role { get; set; }
    public string   Text { get; set; } = string.Empty;
    public bool     IsStreaming { get; set; }
    public List<SourceRef> Sources { get; set; } = [];
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class SourceRef
{
    public string Source { get; set; } = string.Empty;
    public string Chapter { get; set; } = string.Empty;
    public string StitchRange { get; set; } = string.Empty;
    public double Score { get; set; }
}

public class ChatSettings
{
    public ChatMode Mode { get; set; } = ChatMode.Remembrancer;
    public int TopK { get; set; } = 6;
    public bool UseStream { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
    public string ForgeBaseUrl { get; set; } = "http://localhost:5000";
}
