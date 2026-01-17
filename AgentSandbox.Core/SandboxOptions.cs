using AgentSandbox.Core.Shell;
using AgentSandbox.Core.Skills;

namespace AgentSandbox.Core;

/// <summary>
/// Configuration options for a sandbox instance.
/// </summary>
public class SandboxOptions
{
    /// <summary>Maximum total size of all files in bytes (default: 100MB).</summary>
    public long MaxTotalSize { get; set; } = 100 * 1024 * 1024;
    
    /// <summary>Maximum size of a single file in bytes (default: 10MB).</summary>
    public long MaxFileSize { get; set; } = 10 * 1024 * 1024;
    
    /// <summary>Maximum number of files/directories (default: 10000).</summary>
    public int MaxNodeCount { get; set; } = 10000;
    
    /// <summary>Command execution timeout (default: 30 seconds).</summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>Initial environment variables.</summary>
    public Dictionary<string, string> Environment { get; set; } = new();
    
    /// <summary>Initial working directory.</summary>
    public string WorkingDirectory { get; set; } = "/";

    /// <summary>Shell command extensions to register.</summary>
    public IEnumerable<IShellCommand> ShellExtensions { get; set; } = Array.Empty<IShellCommand>();

    /// <summary>
    /// Agent skills to mount into the sandbox filesystem.
    /// Skills are copied to /.sandbox/skills/{name}/ at initialization.
    /// </summary>
    public IReadOnlyList<AgentSkill> Skills { get; set; } = [];

    /// <summary>
    /// Base path where skills are mounted. Default: /.sandbox/skills
    /// </summary>
    public string SkillsMountPath { get; set; } = "/.sandbox/skills";

    /// <summary>
    /// Creates a shallow copy of this options instance.
    /// </summary>
    public SandboxOptions Clone() => new()
    {
        MaxTotalSize = MaxTotalSize,
        MaxFileSize = MaxFileSize,
        MaxNodeCount = MaxNodeCount,
        CommandTimeout = CommandTimeout,
        Environment = new Dictionary<string, string>(Environment),
        WorkingDirectory = WorkingDirectory,
        ShellExtensions = ShellExtensions.ToArray(),
        Skills = Skills.ToArray(),
        SkillsMountPath = SkillsMountPath
    };
}