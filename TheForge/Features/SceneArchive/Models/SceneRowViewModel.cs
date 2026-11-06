namespace TheForge.Features.SceneArchive.Models;

public record SceneRowViewModel(
    string Key,
    string DisplayLabel,
    ScrollDirection Direction,
    List<SceneCardSummary> Cards);
