using AgentSandbox.Core.Mounting;
using AgentSandbox.Core.Shell;
using AgentSandbox.Core.Skills;
using AgentSandbox.Core.Telemetry;

namespace AgentSandbox.Core;

/// <summary>
/// Configuration options for a sandbox instance.
/// </summary>
public class SandboxOptions
{
    /// <summary>Maximum total size of all files in bytes (default: 256KB).</summary>
    public long MaxTotalSize { get; set; } = 256 * 1024;
    
    /// <summary>Maximum size of a single file in bytes (default: 16KB).</summary>
    public long MaxFileSize { get; set; } = 16 * 1024;
    
    /// <summary>Maximum number of files/directories (default: 1000).</summary>
    public int MaxNodeCount { get; set; } = 1000;
    
    /// <summary>Command execution timeout (default: 30 seconds).</summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>Initial environment variables.</summary>
    public Dictionary<string, string> Environment { get; set; } = new();
    
    /// <summary>Initial working directory.</summary>
    public string WorkingDirectory { get; set; } = "/";

    /// <summary>Shell command extensions to register.</summary>
    public IEnumerable<IShellCommand> ShellExtensions { get; set; } = Array.Empty<IShellCommand>();

    /// <summary>
    /// Files to mount into the sandbox filesystem at initialization.
    /// Each mount specifies a destination path and file source.
    /// </summary>
    public IReadOnlyList<FileMountOptions> Mounts { get; set; } = [];

    /// <summary>
    /// Agent skills configuration. Skills are mounted at initialization.
    /// </summary>
    public AgentSkillOptions AgentSkills { get; set; } = new();

    /// <summary>
    /// Telemetry configuration. Default: disabled (opt-in).
    /// </summary>
    public SandboxTelemetryOptions? Telemetry { get; set; }

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
        Mounts = Mounts.ToArray(),
        AgentSkills = new AgentSkillOptions
        {
            Skills = AgentSkills.Skills.ToArray(),
            MountPath = AgentSkills.MountPath
        },
        Telemetry = Telemetry
    };
}