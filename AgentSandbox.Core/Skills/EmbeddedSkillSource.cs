using System.Reflection;

namespace AgentSandbox.Core.Skills;

/// <summary>
/// Loads skill files from embedded resources in an assembly.
/// </summary>
public class EmbeddedSkillSource : ISkillSource
{
    private readonly Assembly _assembly;
    private readonly string _resourcePrefix;

    /// <summary>
    /// Creates a skill source from embedded resources.
    /// </summary>
    /// <param name="assembly">The assembly containing embedded resources.</param>
    /// <param name="resourcePrefix">
    /// The resource name prefix (e.g., "MyApp.Skills.PythonDev").
    /// Resources should be named like "MyApp.Skills.PythonDev.SKILL.md".
    /// </param>
    public EmbeddedSkillSource(Assembly assembly, string resourcePrefix)
    {
        _assembly = assembly;
        _resourcePrefix = resourcePrefix.TrimEnd('.');
    }

    public IEnumerable<SkillFile> GetFiles()
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
            // e.g., "MyApp.Skills.PythonDev.scripts.setup.sh" -> "scripts/setup.sh"
            var relativePath = ConvertResourceNameToPath(resourceName, prefix);

            yield return new SkillFile
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
        // e.g., "SKILL.md" -> "SKILL.md"
        
        var parts = path.Split('.');
        if (parts.Length <= 2)
        {
            // Simple case: "SKILL.md" or just "file"
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
