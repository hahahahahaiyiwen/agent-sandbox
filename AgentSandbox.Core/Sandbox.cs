using System.Diagnostics;
using System.Text;
using AgentSandbox.Core.FileSystem;
using AgentSandbox.Core.Importing;
using AgentSandbox.Core.Shell;
using AgentSandbox.Core.Skills;
using AgentSandbox.Core.Telemetry;

namespace AgentSandbox.Core;

/// <summary>
/// Represents a sandboxed execution environment with virtual filesystem and shell.
/// </summary>
public class Sandbox : IDisposable, IObservableSandbox
{
    private readonly FileSystem.FileSystem _fileSystem;
    private readonly SandboxShell _shell;
    private readonly SandboxOptions _options;
    private readonly List<ShellResult> _commandHistory = new();
    private readonly List<SkillInfo> _loadedSkills = new();
    private readonly List<ISandboxObserver> _observers = new();
    private readonly object _observerLock = new();
    private readonly Action<string>? _onDisposed;
    private readonly Activity? _sandboxActivity;
    private bool _disposed;

    private bool TelemetryEnabled => _options.Telemetry?.Enabled == true;

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

    public Sandbox(string? id = null, SandboxOptions? options = null)
        : this(id, options, null)
    {
    }

    internal Sandbox(string? id, SandboxOptions? options, Action<string>? onDisposed)
    {
        Id = id ?? Guid.NewGuid().ToString("N")[..12];
        _options = options ?? new SandboxOptions();
        _onDisposed = onDisposed;

        // Start sandbox-level activity for tracing
        if (TelemetryEnabled)
        {
            _sandboxActivity = SandboxTelemetry.StartSandboxActivity(Id);
            var instanceId = _options.Telemetry!.InstanceId;
            SandboxTelemetry.SandboxesCreated.Add(1, 
                new KeyValuePair<string, object?>("sandbox.id", Id),
                new KeyValuePair<string, object?>("service.instance.id", instanceId));
            SandboxTelemetry.ActiveSandboxes.Add(1,
                new KeyValuePair<string, object?>("service.instance.id", instanceId));
        }
        
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

        // Import files
        ImportFiles();

        // Load agent skills
        LoadSkills();

        // Emit lifecycle event
        if (TelemetryEnabled)
        {
            EmitLifecycleEvent(SandboxLifecycleType.Created);
        }
    }

    /// <summary>
    /// Gets information about all loaded skills.
    /// </summary>
    public IReadOnlyList<SkillInfo> GetSkills() => _loadedSkills.AsReadOnly();

    /// <summary>
    /// Gets a description of loaded skills for use in AI function descriptions.
    /// Uses the XML format recommended by agentskills.io specification.
    /// </summary>
    public string GetSkillsDescription()
    {
        if (_loadedSkills.Count == 0)
        {
            return "No skills are currently available.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("<available_skills>");

        foreach (var skill in _loadedSkills)
        {
            sb.AppendLine("  <skill>");
            sb.AppendLine($"    <name>{skill.Name}</name>");
            sb.AppendLine($"    <description>{skill.Description}</description>");
            sb.AppendLine($"    <location>{skill.Path}/SKILL.md</location>");
            sb.AppendLine("  </skill>");
        }

        sb.AppendLine("</available_skills>");

        return sb.ToString();
    }
    
    private void LoadSkills()
    {
        if (_options.AgentSkills.Skills.Count == 0) return;

        // Create skills base directory
        _fileSystem.CreateDirectory(_options.AgentSkills.BasePath);

        foreach (var skill in _options.AgentSkills.Skills)
        {
            var skillInfo = LoadSkill(skill);
            _loadedSkills.Add(skillInfo);
        }
    }

    private SkillInfo LoadSkill(AgentSkill skill)
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
        var skillPath = $"{_options.AgentSkills.BasePath}/{skillName}";

        // Copy files to the path
        CopyFilesInternal(skillPath, files);

        return new SkillInfo
        {
            Name = skillName,
            Description = metadata.Description,
            Path = skillPath,
            Metadata = metadata
        };
    }

    private void ImportFiles()
    {
        foreach (var import in _options.Imports)
        {
            var files = import.Source.GetFiles().ToList();
            CopyFilesInternal(import.Path, files);
        }
    }

    private void CopyFilesInternal(string destPath, IReadOnlyList<FileData> files)
    {
        // Normalize path
        if (!destPath.StartsWith("/"))
        {
            destPath = "/" + destPath;
        }

        // Create destination directory
        _fileSystem.CreateDirectory(destPath);

        // Copy all files to virtual filesystem
        foreach (var file in files)
        {
            var filePath = $"{destPath}/{file.RelativePath}";
            
            // Ensure parent directory exists
            var lastSlash = filePath.LastIndexOf('/');
            if (lastSlash > 0)
            {
                var parentDir = filePath[..lastSlash];
                if (parentDir != destPath && !_fileSystem.Exists(parentDir))
                {
                    _fileSystem.CreateDirectory(parentDir);
                }
            }

            _fileSystem.WriteFile(filePath, file.Content);
        }
    }

    /// <summary>
    /// Executes a shell command in the sandbox.
    /// </summary>
    public ShellResult Execute(string command)
    {
        ThrowIfDisposed();
        LastActivityAt = DateTime.UtcNow;

        Activity? activity = null;
        var stopwatch = Stopwatch.StartNew();

        // Start tracing span if telemetry enabled
        if (TelemetryEnabled && _options.Telemetry!.TraceCommands)
        {
            activity = SandboxTelemetry.StartCommandActivity(command, Id);
            activity?.SetTag("command.cwd", _shell.CurrentDirectory);
        }

        try
        {
            var result = _shell.Execute(command);
            _commandHistory.Add(result);
            stopwatch.Stop();

            // Record metrics and emit events
            if (TelemetryEnabled)
            {
                var commandName = SandboxTelemetry.GetCommandName(command);
                var tags = new TagList
                {
                    { "sandbox.id", Id },
                    { "command.name", commandName },
                    { "service.instance.id", _options.Telemetry!.InstanceId }
                };

                SandboxTelemetry.CommandsExecuted.Add(1, tags);
                SandboxTelemetry.CommandDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);

                if (!result.Success)
                {
                    SandboxTelemetry.CommandsFailed.Add(1, tags);
                }

                activity?.SetTag("command.exit_code", result.ExitCode);
                activity?.SetTag("command.duration_ms", stopwatch.Elapsed.TotalMilliseconds);

                EmitCommandExecutedEvent(command, result, stopwatch.Elapsed);
            }

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            if (TelemetryEnabled)
            {
                EmitErrorEvent("Command", ex.Message, ex);
            }
            
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
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
    /// Gets a description of the sandbox tool for use in AI function descriptions.
    /// Dynamically includes all available commands and extensions.
    /// </summary>
    public string GetToolDescription()
    {
        var sb = new StringBuilder();
        
        sb.Append("Run shell commands. ");
        sb.Append($"Available commands: {string.Join(", ", _shell.GetAvailableCommands())}. ");
        sb.Append("Run 'help' to list commands or '<command> -h' for detailed help on a specific command. ");
        sb.Append("Commands use short-style arguments (e.g., -l, -a, -n). ");

        return sb.ToString();
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

    #region Observer Pattern

    /// <summary>
    /// Subscribes an observer to receive sandbox events.
    /// </summary>
    public IDisposable Subscribe(ISandboxObserver observer)
    {
        lock (_observerLock)
        {
            _observers.Add(observer);
        }
        return new ObserverUnsubscriber(this, observer);
    }

    private void Unsubscribe(ISandboxObserver observer)
    {
        lock (_observerLock)
        {
            _observers.Remove(observer);
        }
    }

    private sealed class ObserverUnsubscriber : IDisposable
    {
        private readonly Sandbox _sandbox;
        private readonly ISandboxObserver _observer;

        public ObserverUnsubscriber(Sandbox sandbox, ISandboxObserver observer)
        {
            _sandbox = sandbox;
            _observer = observer;
        }

        public void Dispose() => _sandbox.Unsubscribe(_observer);
    }

    #endregion

    #region Event Emission

    private void EmitCommandExecutedEvent(string command, ShellResult result, TimeSpan duration)
    {
        if (_observers.Count == 0) return;

        var maxOutput = _options.Telemetry?.MaxOutputLength ?? 1024;
        var evt = new CommandExecutedEvent
        {
            SandboxId = Id,
            Command = command,
            CommandName = SandboxTelemetry.GetCommandName(command),
            ExitCode = result.ExitCode,
            Duration = duration,
            Output = TruncateOutput(result.Stdout, maxOutput),
            Error = result.Stderr,
            WorkingDirectory = _shell.CurrentDirectory,
            TraceId = Activity.Current?.TraceId.ToString(),
            SpanId = Activity.Current?.SpanId.ToString()
        };

        NotifyObservers(o => o.OnCommandExecuted(evt));
    }

    private void EmitLifecycleEvent(SandboxLifecycleType lifecycleType, string? details = null)
    {
        if (_observers.Count == 0 && !TelemetryEnabled) return;

        var evt = new SandboxLifecycleEvent
        {
            SandboxId = Id,
            LifecycleType = lifecycleType,
            Details = details,
            TraceId = Activity.Current?.TraceId.ToString(),
            SpanId = Activity.Current?.SpanId.ToString()
        };

        NotifyObservers(o => o.OnLifecycleEvent(evt));
    }

    private void EmitErrorEvent(string category, string message, Exception? ex = null)
    {
        if (_observers.Count == 0) return;

        var evt = new SandboxErrorEvent
        {
            SandboxId = Id,
            Category = category,
            Message = message,
            ExceptionType = ex?.GetType().Name,
            StackTrace = ex?.StackTrace,
            TraceId = Activity.Current?.TraceId.ToString(),
            SpanId = Activity.Current?.SpanId.ToString()
        };

        NotifyObservers(o => o.OnError(evt));
    }

    private void NotifyObservers(Action<ISandboxObserver> action)
    {
        ISandboxObserver[] observers;
        lock (_observerLock)
        {
            if (_observers.Count == 0) return;
            observers = _observers.ToArray();
        }

        foreach (var observer in observers)
        {
            try
            {
                action(observer);
            }
            catch
            {
                // Don't let observer exceptions affect sandbox operation
            }
        }
    }

    private static string? TruncateOutput(string? output, int maxLength)
    {
        if (output == null || output.Length <= maxLength)
            return output;
        return output[..maxLength] + "... (truncated)";
    }

    #endregion

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Sandbox));
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;

        // Emit lifecycle event before disposing activity
        if (TelemetryEnabled)
        {
            EmitLifecycleEvent(SandboxLifecycleType.Disposed);

            var instanceId = _options.Telemetry!.InstanceId;
            SandboxTelemetry.SandboxesDisposed.Add(1,
                new KeyValuePair<string, object?>("sandbox.id", Id),
                new KeyValuePair<string, object?>("service.instance.id", instanceId));
            SandboxTelemetry.ActiveSandboxes.Add(-1,
                new KeyValuePair<string, object?>("service.instance.id", instanceId));
            
            // End sandbox-level activity
            _sandboxActivity?.SetTag("sandbox.command_count", _commandHistory.Count);
            _sandboxActivity?.Dispose();
        }

        _commandHistory.Clear();
        
        lock (_observerLock)
        {
            _observers.Clear();
        }
        
        // Notify manager to remove reference
        _onDisposed?.Invoke(Id);
        
        GC.SuppressFinalize(this);
    }
}
