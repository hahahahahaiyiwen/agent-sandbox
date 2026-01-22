namespace AgentSandbox.Core.Importing;

/// <summary>
/// Represents a file to be imported into the sandbox filesystem.
/// </summary>
public class FileData
{
    /// <summary>
    /// Relative path within the source (e.g., "SKILL.md", "scripts/setup.sh", "data/config.json").
    /// Uses forward slashes as path separator.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// File content as bytes.
    /// </summary>
    public required byte[] Content { get; init; }

    /// <summary>
    /// Gets the content as a UTF-8 string.
    /// </summary>
    public string GetContentAsString() => System.Text.Encoding.UTF8.GetString(Content);
}
