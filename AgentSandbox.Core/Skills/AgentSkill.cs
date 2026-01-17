using System.Reflection;

namespace AgentSandbox.Core.Skills;

/// <summary>
/// Represents an agent skill to be mounted into the sandbox filesystem.
/// Skills are folders containing SKILL.md (required), scripts/, references/, and assets/.
/// </summary>
public class AgentSkill
{
    /// <summary>
    /// Unique name for the skill. Used as the folder name under /.sandbox/skills/
    /// If not provided, will be extracted from SKILL.md frontmatter.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Source for loading skill files.
    /// </summary>
    public required ISkillSource Source { get; init; }

    /// <summary>
    /// Creates a skill from a local filesystem path.
    /// </summary>
    /// <param name="path">Path to the skill folder containing SKILL.md.</param>
    /// <param name="name">Optional name override. If not provided, uses name from SKILL.md.</param>
    public static AgentSkill FromPath(string path, string? name = null)
    {
        return new AgentSkill
        {
            Name = name,
            Source = new FileSystemSkillSource(path)
        };
    }

    /// <summary>
    /// Creates a skill from embedded assembly resources.
    /// </summary>
    /// <param name="assembly">The assembly containing embedded resources.</param>
    /// <param name="resourcePrefix">
    /// The resource name prefix (e.g., "MyApp.Skills.PythonDev").
    /// Resources should be embedded with names like "MyApp.Skills.PythonDev.SKILL.md".
    /// </param>
    /// <param name="name">Optional name override. If not provided, uses name from SKILL.md.</param>
    public static AgentSkill FromAssembly(Assembly assembly, string resourcePrefix, string? name = null)
    {
        return new AgentSkill
        {
            Name = name,
            Source = new EmbeddedSkillSource(assembly, resourcePrefix)
        };
    }

    /// <summary>
    /// Creates a skill from in-memory files. Useful for testing.
    /// </summary>
    /// <param name="files">Dictionary of relative path to file content.</param>
    /// <param name="name">Optional name override. If not provided, uses name from SKILL.md.</param>
    public static AgentSkill FromFiles(IDictionary<string, string> files, string? name = null)
    {
        return new AgentSkill
        {
            Name = name,
            Source = new InMemorySkillSource(files)
        };
    }

    /// <summary>
    /// Creates a skill from an InMemorySkillSource. Useful for fluent building.
    /// </summary>
    /// <param name="source">The in-memory skill source.</param>
    /// <param name="name">Optional name override. If not provided, uses name from SKILL.md.</param>
    public static AgentSkill FromSource(ISkillSource source, string? name = null)
    {
        return new AgentSkill
        {
            Name = name,
            Source = source
        };
    }
}
