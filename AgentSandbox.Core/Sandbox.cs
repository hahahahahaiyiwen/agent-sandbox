using System.Text;
using AgentSandbox.Core.FileSystem;
using AgentSandbox.Core.Shell;
using AgentSandbox.Core.Skills;

namespace AgentSandbox.Core;

/// <summary>
/// Represents a sandboxed execution environment with virtual filesystem and shell.
/// </summary>
public class Sandbox : IDisposable
{
    private readonly FileSystem.FileSystem _fileSystem;
    private readonly SandboxShell _shell;
    private readonly SandboxOptions _options;
    private readonly List<ShellResult> _commandHistory = new();
    private readonly List<SkillInfo> _mountedSkills = new();
    private readonly Action<string>? _onDisposed;
    private bool _disposed;

    public string Id { get; }
    public DateTime CreatedAt { get; }
    public DateTime LastActivityAt { get; private set; }
    
    /// <summary>
    /// Gets the current working directory of the sandbox shell.
    /// </summary>
    public string CurrentDirectory => _shell.CurrentDirectory;
    
    /// <summary>
    /// Gets a copy of the sandbox options. Modifications to the returned object do not affect the sandbox.
    /// </summary>
    public SandboxOptions Options => _options.Clone();

    public Sandbox(string? id = null, SandboxOptions? options = null, Action<string>? onDisposed = null)
    {
        Id = id ?? Guid.NewGuid().ToString("N")[..12];
        _options = options ?? new SandboxOptions();
        _onDisposed = onDisposed;
        
        // Create filesystem with size limits from options
        var fsOptions = new FileSystemOptions
        {
            MaxTotalSize = _options.MaxTotalSize,
            MaxFileSize = _options.MaxFileSize,
            MaxNodeCount = _options.MaxNodeCount
        };
        _fileSystem = new FileSystem.FileSystem(fsOptions);
        _shell = new SandboxShell(_fileSystem);
        CreatedAt = DateTime.UtcNow;
        LastActivityAt = CreatedAt;

        // Apply initial environment
        foreach (var kvp in _options.Environment)
        {
            _shell.Execute($"export {kvp.Key}={kvp.Value}");
        }

        // Set initial working directory
        if (_options.WorkingDirectory != "/")
        {
            _fileSystem.CreateDirectory(_options.WorkingDirectory);
            _shell.Execute($"cd {_options.WorkingDirectory}");
        }

        // Register shell extensions
        foreach (var cmd in _options.ShellExtensions)
        {
            _shell.RegisterCommand(cmd);
        }

        // Mount agent skills
        MountSkills();
    }

    /// <summary>
    /// Gets information about all mounted skills.
    /// </summary>
    public IReadOnlyList<SkillInfo> GetMountedSkills() => _mountedSkills.AsReadOnly();
    
    private void MountSkills()
    {
        if (_options.Skills.Count == 0) return;

        // Create skills base directory
        _fileSystem.CreateDirectory(_options.SkillsMountPath);

        foreach (var skill in _options.Skills)
        {
            var skillInfo = MountSkill(skill);
            _mountedSkills.Add(skillInfo);
        }
    }

    private SkillInfo MountSkill(AgentSkill skill)
    {
        // Get all files from the skill source
        var files = skill.Source.GetFiles().ToList();

        // Find and parse SKILL.md (required)
        var skillMdFile = files.FirstOrDefault(f => 
            f.RelativePath.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase));
        
        if (skillMdFile == null)
        {
            throw new InvalidSkillException("Skill must contain a SKILL.md file");
        }

        var metadata = SkillMetadata.Parse(skillMdFile.GetContentAsString());

        // Use name from AgentSkill if provided, otherwise from SKILL.md
        var skillName = skill.Name ?? metadata.Name;
        var mountPath = $"{_options.SkillsMountPath}/{skillName}";

        // Create mount directory
        _fileSystem.CreateDirectory(mountPath);

        // Copy all files to virtual filesystem
        foreach (var file in files)
        {
            var destPath = $"{mountPath}/{file.RelativePath}";
            
            // Ensure parent directory exists
            var parentDir = destPath[..destPath.LastIndexOf('/')];
            if (parentDir != mountPath && !_fileSystem.Exists(parentDir))
            {
                _fileSystem.CreateDirectory(parentDir);
            }

            _fileSystem.WriteFile(destPath, file.Content);
        }

        return new SkillInfo
        {
            Name = skillName,
            Description = metadata.Description,
            MountPath = mountPath,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Executes a shell command in the sandbox.
    /// </summary>
    public ShellResult Execute(string command)
    {
        ThrowIfDisposed();
        LastActivityAt = DateTime.UtcNow;
        
        var result = _shell.Execute(command);
        _commandHistory.Add(result);
        
        return result;
    }

    /// <summary>
    /// Gets command execution history.
    /// </summary>
    public IReadOnlyList<ShellResult> GetHistory() => _commandHistory.AsReadOnly();

    /// <summary>
    /// Creates a snapshot of the entire sandbox state.
    /// </summary>
    public SandboxSnapshot CreateSnapshot()
    {
        ThrowIfDisposed();
        return new SandboxSnapshot
        {
            Id = Id,
            FileSystemData = _fileSystem.CreateSnapshot(),
            CurrentDirectory = _shell.CurrentDirectory,
            Environment = new Dictionary<string, string>(_shell.Environment),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Restores sandbox state from a snapshot.
    /// </summary>
    public void RestoreSnapshot(SandboxSnapshot snapshot)
    {
        ThrowIfDisposed();
        _fileSystem.RestoreSnapshot(snapshot.FileSystemData);
        _shell.Execute($"cd {snapshot.CurrentDirectory}");
        
        foreach (var kvp in snapshot.Environment)
        {
            _shell.Execute($"export {kvp.Key}={kvp.Value}");
        }
        
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets sandbox statistics.
    /// </summary>
    public SandboxStats GetStats() => new()
    {
        Id = Id,
        FileCount = _fileSystem.NodeCount,
        TotalSize = _fileSystem.TotalSize, // in bytes
        CommandCount = _commandHistory.Count,
        CurrentDirectory = _shell.CurrentDirectory,
        CreatedAt = CreatedAt,
        LastActivityAt = LastActivityAt
    };

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Sandbox));
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _commandHistory.Clear();
        
        // Notify manager to remove reference
        _onDisposed?.Invoke(Id);
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Snapshot of sandbox state for persistence/restoration.
/// </summary>
public class SandboxSnapshot
{
    public string Id { get; set; } = string.Empty;
    public byte[] FileSystemData { get; set; } = Array.Empty<byte>();
    public string CurrentDirectory { get; set; } = "/";
    public Dictionary<string, string> Environment { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Runtime statistics for a sandbox.
/// </summary>
public class SandboxStats
{
    public string Id { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long TotalSize { get; set; }
    public int CommandCount { get; set; }
    public string CurrentDirectory { get; set; } = "/";
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
}
