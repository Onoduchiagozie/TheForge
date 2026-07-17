namespace TheForge.Services.SceneArchive;

/// <summary>
/// Deterministic accent color assignment. Any scene_type value — known ahead of time or not —
/// always maps to the same accent slot, so the CSS palette (Phase 7) only needs N fixed
/// classes (accent-0..accent-{N-1}) instead of a lookup table keyed by every possible category
/// string that might exist in the data.
///
/// Uses FNV-1a instead of string.GetHashCode() because .NET's built-in hash is randomized per
/// process by design — it would assign a different accent to the same category on every app
/// restart, which is exactly the flicker this exists to avoid.
/// </summary>
public static class AccentPalette
{
    public const int PaletteSize = 8;

    public static int GetAccentIndex(string sceneType)
    {
        unchecked
        {
            const uint fnvOffsetBasis = 2166136261;
            const uint fnvPrime = 16777619;

            var hash = fnvOffsetBasis;
            foreach (var c in sceneType)
            {
                hash ^= c;
                hash *= fnvPrime;
            }

            return (int)(hash % PaletteSize);
        }
    }
}
