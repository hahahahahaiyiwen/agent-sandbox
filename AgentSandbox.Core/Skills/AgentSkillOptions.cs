namespace AgentSandbox.Core.Skills;

/// <summary>
/// Configuration options for agent skills.
/// </summary>
public class AgentSkillOptions
{
    /// <summary>
    /// Agent skills to load into the sandbox filesystem.
    /// Skills are copied to {BasePath}/{name}/ at initialization.
    /// </summary>
    public IReadOnlyList<AgentSkill> Skills { get; set; } = [];

    /// <summary>
    /// Base path where skills are installed. Default: /.sandbox/skills
    /// </summary>
    public string BasePath { get; set; } = "/.sandbox/skills";
}
