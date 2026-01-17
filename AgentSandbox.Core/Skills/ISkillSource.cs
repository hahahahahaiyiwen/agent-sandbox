namespace AgentSandbox.Core.Skills;

/// <summary>
/// Represents a file within a skill package.
/// </summary>
public class SkillFile
{
    /// <summary>
    /// Relative path within the skill (e.g., "SKILL.md", "scripts/setup.sh").
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

/// <summary>
/// Abstraction for loading skill files from various sources.
/// </summary>
public interface ISkillSource
{
    /// <summary>
    /// Gets all files in the skill package.
    /// </summary>
    IEnumerable<SkillFile> GetFiles();
}
