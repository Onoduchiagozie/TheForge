namespace TheForge.Features.Workspace.Models;

public sealed record PromptChip(string Text, string? BookHint = null);

public static class WorkspacePrompts
{
    public static PromptChip[] PickNarrator() => PickThree([
        new("Fulgrim's sensations as the daemon in the blade took hold while dueling his brother Ferrus Manus", "Fulgrim"),
        new("Leman Russ breaks Magnus back on prospero", "A Thousand Sons"),
        new("Kakophoni use sonic weapon against white scars", "Fulgrim"),
        new("the destruction of Prospero", "A Thousand Sons"),
        new("the World Eaters' massacre at Nuceria", "Betrayer"),
        new("Magnus crashes wards on the imperial palace ", "A Thousand Sons"),
        new("Angron Warp Ascension", "Betrayer")
    ]);

    public static PromptChip[] PickExplorer() => [new("why did horus turn traitor")];

    private static PromptChip[] PickThree(PromptChip[] pool) =>
        pool.OrderBy(_ => Random.Shared.Next()).Take(3).ToArray();
}
