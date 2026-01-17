namespace AgentSandbox.Core.Skills;

/// <summary>
/// Loads skill files from a local filesystem directory.
/// </summary>
public class FileSystemSkillSource : ISkillSource
{
    private readonly string _rootPath;

    public FileSystemSkillSource(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Skill directory not found: {rootPath}");
        }
        _rootPath = rootPath;
    }

    public IEnumerable<SkillFile> GetFiles()
    {
        return GetFilesRecursive(_rootPath, "");
    }

    private IEnumerable<SkillFile> GetFilesRecursive(string currentPath, string relativePath)
    {
        // Get files in current directory
        foreach (var file in Directory.GetFiles(currentPath))
        {
            var fileName = Path.GetFileName(file);
            var relPath = string.IsNullOrEmpty(relativePath) 
                ? fileName 
                : $"{relativePath}/{fileName}";

            yield return new SkillFile
            {
                RelativePath = relPath,
                Content = File.ReadAllBytes(file)
            };
        }

        // Recurse into subdirectories
        foreach (var dir in Directory.GetDirectories(currentPath))
        {
            var dirName = Path.GetFileName(dir);
            var relDir = string.IsNullOrEmpty(relativePath) 
                ? dirName 
                : $"{relativePath}/{dirName}";

            foreach (var file in GetFilesRecursive(dir, relDir))
            {
                yield return file;
            }
        }
    }
}
