using System.Text.RegularExpressions;

namespace AgentSandbox.Core.Skills;

/// <summary>
/// Skill metadata parsed from SKILL.md frontmatter.
/// Follows the agentskills.io specification.
/// 
/// SKILL.md format:
/// ---
/// name: skill-name
/// description: Skill description here
/// ---
/// 
/// # Skill Title
/// Instructions...
/// </summary>
public partial class SkillMetadata
{
    /// <summary>
    /// Skill name from frontmatter. Required.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Skill description from frontmatter. Required.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Full markdown content of SKILL.md (including frontmatter).
    /// </summary>
    public string? RawContent { get; init; }

    /// <summary>
    /// Instructions content (markdown after frontmatter).
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// Parses SKILL.md content to extract metadata and instructions.
    /// </summary>
    /// <exception cref="InvalidSkillException">Thrown when SKILL.md is missing required frontmatter fields.</exception>
    public static SkillMetadata Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidSkillException("SKILL.md content is empty");
        }

        string? name = null;
        string? description = null;
        string instructions = content;

        // Check for YAML frontmatter: starts with --- and ends with ---
        var frontmatterMatch = FrontmatterRegex().Match(content);
        if (!frontmatterMatch.Success)
        {
            throw new InvalidSkillException("SKILL.md must contain YAML frontmatter (--- ... ---)");
        }

        var frontmatter = frontmatterMatch.Groups[1].Value;
        instructions = content[(frontmatterMatch.Length)..].TrimStart('\r', '\n');

        // Parse YAML frontmatter (simple key: value parsing)
        foreach (var line in frontmatter.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0) continue;

            var key = trimmed[..colonIndex].Trim().ToLowerInvariant();
            var value = trimmed[(colonIndex + 1)..].Trim();

            switch (key)
            {
                case "name":
                    name = value;
                    break;
                case "description":
                    description = value;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidSkillException("SKILL.md frontmatter must contain 'name' field");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new InvalidSkillException("SKILL.md frontmatter must contain 'description' field");
        }

        return new SkillMetadata
        {
            Name = name,
            Description = description,
            RawContent = content,
            Instructions = instructions
        };
    }

    [GeneratedRegex(@"^---\s*\n(.*?)\n---\s*\n?", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();
}

/// <summary>
/// Exception thrown when a skill is invalid or malformed.
/// </summary>
public class InvalidSkillException : Exception
{
    public InvalidSkillException(string message) : base(message) { }
    public InvalidSkillException(string message, Exception innerException) : base(message, innerException) { }
}

