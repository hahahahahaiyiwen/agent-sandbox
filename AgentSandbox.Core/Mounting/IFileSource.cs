namespace AgentSandbox.Core.Mounting;

/// <summary>
/// Abstraction for loading files from various sources (filesystem, embedded resources, in-memory).
/// Used for mounting files into the sandbox, including agent skills.
/// </summary>
public interface IFileSource
{
    /// <summary>
    /// Gets all files from the source.
    /// </summary>
    IEnumerable<FileData> GetFiles();
}
