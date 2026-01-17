namespace AgentSandbox.Core.Skills;

/// <summary>
/// Runtime information about a skill mounted in the sandbox.
/// </summary>
public class SkillInfo
{
    /// <summary>
    /// Skill name from SKILL.md frontmatter.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Skill description from SKILL.md frontmatter.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Mount path in the virtual filesystem (e.g., /.sandbox/skills/my-skill).
    /// </summary>
    public required string MountPath { get; init; }

    /// <summary>
    /// Parsed metadata from SKILL.md.
    /// </summary>
    public required SkillMetadata Metadata { get; init; }
}
