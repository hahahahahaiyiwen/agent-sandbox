namespace AgentSandbox.Core.Skills;

/// <summary>
/// Configuration options for agent skills mounting.
/// </summary>
public class AgentSkillOptions
{
    /// <summary>
    /// Agent skills to mount into the sandbox filesystem.
    /// Skills are copied to {MountPath}/{name}/ at initialization.
    /// </summary>
    public IReadOnlyList<AgentSkill> Skills { get; set; } = [];

    /// <summary>
    /// Base path where skills are mounted. Default: /.sandbox/skills
    /// </summary>
    public string MountPath { get; set; } = "/.sandbox/skills";
}
