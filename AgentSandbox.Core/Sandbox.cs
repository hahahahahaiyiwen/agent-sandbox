using AgentSandbox.Core.FileSystem;
using AgentSandbox.Core.Shell;

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
