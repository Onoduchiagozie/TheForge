namespace TheForge.Services.SceneArchive.Dtos;

public record SceneRowViewModel(
    string Key,
    string DisplayLabel,
    ScrollDirection Direction,
    List<SceneCardSummary> Cards);
