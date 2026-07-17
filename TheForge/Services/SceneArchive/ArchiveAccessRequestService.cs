namespace TheForge.Services.SceneArchive;

/// <summary>
/// PLACEHOLDER per the master prompt: "Clicking Request Access invokes backend workflow
/// placeholder for email notification." There's no SMTP/ticketing backend to wire into yet,
/// so this logs the request — the button-to-service plumbing is real and swappable the moment
/// a real notification channel exists; nothing fake is pretending to send an email today.
/// </summary>
public interface IArchiveAccessRequestService
{
    Task RequestAccessAsync(CancellationToken ct = default);
}

public class ArchiveAccessRequestService(ILogger<ArchiveAccessRequestService> logger) : IArchiveAccessRequestService
{
    public Task RequestAccessAsync(CancellationToken ct = default)
    {
        // TODO: replace with a real notification (email / queue / ticket) once a backend exists.
        logger.LogInformation("Archive access requested at {RequestedAt}", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}
