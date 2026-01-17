using System.Text;

namespace AgentSandbox.Core.Skills;

/// <summary>
/// Loads skill files from in-memory dictionary. Useful for testing and dynamic skill creation.
/// </summary>
public class InMemorySkillSource : ISkillSource
{
    private readonly Dictionary<string, byte[]> _files = new();

    /// <summary>
    /// Creates an empty in-memory skill source.
    /// </summary>
    public InMemorySkillSource()
    {
    }

    /// <summary>
    /// Creates an in-memory skill source with string content.
    /// </summary>
    /// <param name="files">Dictionary of relative path to string content.</param>
    public InMemorySkillSource(IDictionary<string, string> files)
    {
        foreach (var (path, content) in files)
        {
            _files[NormalizePath(path)] = Encoding.UTF8.GetBytes(content);
        }
    }

    /// <summary>
    /// Creates an in-memory skill source with byte content.
    /// </summary>
    /// <param name="files">Dictionary of relative path to byte content.</param>
    public InMemorySkillSource(IDictionary<string, byte[]> files)
    {
        foreach (var (path, content) in files)
        {
            _files[NormalizePath(path)] = content;
        }
    }

    /// <summary>
    /// Adds a file with string content.
    /// </summary>
    public InMemorySkillSource AddFile(string relativePath, string content)
    {
        _files[NormalizePath(relativePath)] = Encoding.UTF8.GetBytes(content);
        return this;
    }

    /// <summary>
    /// Adds a file with byte content.
    /// </summary>
    public InMemorySkillSource AddFile(string relativePath, byte[] content)
    {
        _files[NormalizePath(relativePath)] = content;
        return this;
    }

    public IEnumerable<SkillFile> GetFiles()
    {
        return _files.Select(kvp => new SkillFile
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
