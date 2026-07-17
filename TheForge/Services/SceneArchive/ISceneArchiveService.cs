using TheForge.Services.SceneArchive.Dtos;

namespace TheForge.Services.SceneArchive;

/// <summary>
/// The only scene-archive dependency the Razor layer should ever inject. Wraps ranking +
/// repository and returns UI-safe DTOs only — no EF entities cross this boundary.
/// </summary>
public interface ISceneArchiveService
{
    /// <summary>All rows for the landing page, in display order.</summary>
    Task<List<SceneRowViewModel>> GetLandingRowsAsync(CancellationToken ct = default);

    /// <summary>Full detail for the fullscreen overlay. Null if the scene id doesn't exist.</summary>
    Task<SceneDetailViewModel?> GetSceneDetailAsync(int sceneId, CancellationToken ct = default);
}
