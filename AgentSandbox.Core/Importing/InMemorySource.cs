using System.Text;

namespace AgentSandbox.Core.Importing;

/// <summary>
/// Loads files from in-memory dictionary. Useful for testing and dynamic content.
/// Can be used for importing any files (skills, data, templates, etc.) into the sandbox.
/// </summary>
public class InMemorySource : IFileSource
{
    private readonly Dictionary<string, byte[]> _files = new();

    /// <summary>
    /// Creates an empty in-memory source.
    /// </summary>
    public InMemorySource()
    {
    }

    /// <summary>
    /// Creates an in-memory source with string content.
    /// </summary>
    /// <param name="files">Dictionary of relative path to string content.</param>
    public InMemorySource(IDictionary<string, string> files)
    {
        foreach (var (path, content) in files)
        {
            _files[NormalizePath(path)] = Encoding.UTF8.GetBytes(content);
        }
    }

    /// <summary>
    /// Creates an in-memory source with byte content.
    /// </summary>
    /// <param name="files">Dictionary of relative path to byte content.</param>
    public InMemorySource(IDictionary<string, byte[]> files)
    {
        foreach (var (path, content) in files)
        {
            _files[NormalizePath(path)] = content;
        }
    }

    /// <summary>
    /// Adds a file with string content.
    /// </summary>
    public InMemorySource AddFile(string relativePath, string content)
    {
        _files[NormalizePath(relativePath)] = Encoding.UTF8.GetBytes(content);
        return this;
    }

    /// <summary>
    /// Adds a file with byte content.
    /// </summary>
    public InMemorySource AddFile(string relativePath, byte[] content)
    {
        _files[NormalizePath(relativePath)] = content;
        return this;
    }

    public IEnumerable<FileData> GetFiles()
    {
        return _files.Select(kvp => new FileData
        {
            RelativePath = kvp.Key,
            Content = kvp.Value
        });
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }
}
