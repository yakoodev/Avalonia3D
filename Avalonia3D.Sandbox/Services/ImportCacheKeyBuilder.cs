using Avalonia3D.Loaders;
using System;
using System.IO;

namespace Avalonia3D.Sandbox.Services;

public static class ImportCacheKeyBuilder
{
    public static string Build(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var normalizedPath = fullPath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/').ToLowerInvariant();
        var policyFingerprint = BuildPolicyFingerprint(ImportValidationConfiguration.CurrentPolicy);

        if (!File.Exists(fullPath))
        {
            return $"gltf:{normalizedPath}:missing:policy={policyFingerprint}";
        }

        var fileInfo = new FileInfo(fullPath);
        return $"gltf:{normalizedPath}:ticks={fileInfo.LastWriteTimeUtc.Ticks}:len={fileInfo.Length}:policy={policyFingerprint}";
    }

    public static string BuildPolicyFingerprint(ImportValidationPolicy policy)
    {
        return policy.ToString().ToLowerInvariant();
    }
}
