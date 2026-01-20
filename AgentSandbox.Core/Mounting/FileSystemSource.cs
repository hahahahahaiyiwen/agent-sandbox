namespace AgentSandbox.Core.Mounting;

/// <summary>
/// Loads files from a local filesystem directory.
/// Can be used for mounting any files (skills, data, templates, etc.) into the sandbox.
/// </summary>
public class FileSystemSource : IFileSource
{
    private readonly string _rootPath;

    public FileSystemSource(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {rootPath}");
        }
        _rootPath = rootPath;
    }

    public IEnumerable<FileData> GetFiles()
    {
        return GetFilesRecursive(_rootPath, "");
    }

    private IEnumerable<FileData> GetFilesRecursive(string currentPath, string relativePath)
    {
        // Get files in current directory
        foreach (var file in Directory.GetFiles(currentPath))
        {
            var fileName = Path.GetFileName(file);
            var relPath = string.IsNullOrEmpty(relativePath) 
                ? fileName 
                : $"{relativePath}/{fileName}";

            yield return new FileData
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
