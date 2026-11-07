using System.Security.Cryptography;

namespace TheForge.Services;

/// <summary>Versions assets by their published bytes, including Blazor's scoped CSS bundle.</summary>
public sealed class StaticAssetUrls(IWebHostEnvironment environment)
{
    public string VersionedPath(string path)
    {
        var file = environment.WebRootFileProvider.GetFileInfo(path);
        if (!file.Exists)
            return path;

        // The file provider supports both development manifests and published files.
        // Content hashes invalidate cached styles even for a CSS-only publish.
        using var stream = file.CreateReadStream();
        return $"{path}?v={Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()}";
    }
}
