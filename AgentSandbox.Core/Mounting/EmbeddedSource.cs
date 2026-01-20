using System.Reflection;

namespace AgentSandbox.Core.Mounting;

/// <summary>
/// Loads files from embedded resources in an assembly.
/// Can be used for mounting any files (skills, data, templates, etc.) into the sandbox.
/// </summary>
public class EmbeddedSource : IFileSource
{
    private readonly Assembly _assembly;
    private readonly string _resourcePrefix;

    /// <summary>
    /// Creates a source from embedded resources.
    /// </summary>
    /// <param name="assembly">The assembly containing embedded resources.</param>
    /// <param name="resourcePrefix">
    /// The resource name prefix (e.g., "MyApp.Resources.Data").
    /// Resources should be named like "MyApp.Resources.Data.config.json".
    /// </param>
    public EmbeddedSource(Assembly assembly, string resourcePrefix)
    {
        _assembly = assembly;
        _resourcePrefix = resourcePrefix.TrimEnd('.');
    }

    public IEnumerable<FileData> GetFiles()
    {
        var prefix = _resourcePrefix + ".";
        var resourceNames = _assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        foreach (var resourceName in resourceNames)
        {
            using var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue;

            using var ms = new MemoryStream();
            stream.CopyTo(ms);

            // Convert resource name to relative path
            // e.g., "MyApp.Resources.Data.scripts.setup.sh" -> "scripts/setup.sh"
            var relativePath = ConvertResourceNameToPath(resourceName, prefix);

            yield return new FileData
            {
                RelativePath = relativePath,
                Content = ms.ToArray()
            };
        }
    }

    private static string ConvertResourceNameToPath(string resourceName, string prefix)
    {
        // Remove prefix
        var path = resourceName[prefix.Length..];

        // Handle file extension - the last dot before extension is the real separator
        // e.g., "scripts.setup.sh" -> "scripts/setup.sh"
        // e.g., "config.json" -> "config.json"
        
        var parts = path.Split('.');
        if (parts.Length <= 2)
        {
            // Simple case: "config.json" or just "file"
            return path;
        }

        // Rebuild path: all parts except last two are directories
        // Last two parts are filename.extension
        var dirs = parts.Take(parts.Length - 2);
        var fileName = parts[^2] + "." + parts[^1];

        var dirPath = string.Join("/", dirs);
        return string.IsNullOrEmpty(dirPath) ? fileName : $"{dirPath}/{fileName}";
    }
}
