# Observability Design

This document describes the observability architecture for AgentSandbox, enabling production monitoring, real-time streaming, and integration with OpenTelemetry-compatible backends.

## Overview

AgentSandbox observability provides comprehensive visibility into sandbox operations through:

1. **Metrics** - Quantitative measurements (command counts, durations, file sizes)
2. **Traces** - Distributed tracing of command execution flows
3. **Logs** - Structured event logs for auditing and debugging
4. **Events** - Real-time streaming of sandbox activities

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           OpenTelemetry Protocol                             │
│                                                                              │
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────┐                   │
│  │   Metrics   │     │   Traces    │     │    Logs     │                   │
│  └──────┬──────┘     └──────┬──────┘     └──────┬──────┘                   │
│         │                   │                   │                           │
│         └───────────────────┼───────────────────┘                           │
│                             │                                                │
│                    ┌────────▼────────┐                                      │
│                    │  OTLP Exporter  │                                      │
│                    └────────┬────────┘                                      │
│                             │                                                │
├─────────────────────────────┼───────────────────────────────────────────────┤
│                             │                                                │
│                    ┌────────▼────────┐                                      │
│                    │ SandboxTelemetry│  ◄── Opt-in instrumentation          │
│                    └────────┬────────┘                                      │
│                             │                                                │
│         ┌───────────────────┼───────────────────┐                           │
│         │                   │                   │                           │
│  ┌──────▼──────┐    ┌───────▼───────┐   ┌──────▼──────┐                    │
│  │   Sandbox   │    │  FileSystem   │   │    Shell    │                    │
│  └─────────────┘    └───────────────┘   └─────────────┘                    │
│                                                                              │
│                         AgentSandbox                                         │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Design Principles

| Principle | Description |
|-----------|-------------|
| **Opt-in** | Zero overhead when telemetry is disabled |
| **OpenTelemetry Native** | Built on OTEL SDK, exports via OTLP |
| **Real-time Streaming** | Events emitted as they occur |
| **Low Overhead** | Minimal performance impact in production |
| **Correlation** | Trace context propagation across operations |

## Architecture

### Telemetry Provider

Central configuration point for enabling observability:

```csharp
public class SandboxTelemetryOptions
{
    /// <summary>
    /// Enable telemetry collection. Default: false (opt-in).
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Service name for telemetry. Default: "AgentSandbox".
    /// </summary>
    public string ServiceName { get; set; } = "AgentSandbox";
    
    /// <summary>
    /// Enable command execution tracing.
    /// </summary>
    public bool TraceCommands { get; set; } = true;
    
    /// <summary>
    /// Enable filesystem operation tracing.
    /// </summary>
    public bool TraceFileSystem { get; set; } = true;
    
    /// <summary>
    /// Enable metrics collection.
    /// </summary>
    public bool CollectMetrics { get; set; } = true;
    
    /// <summary>
    /// Enable structured logging.
    /// </summary>
    public bool EnableLogging { get; set; } = true;
    
    /// <summary>
    /// Minimum command duration to trace (filters noise).
    /// </summary>
    public TimeSpan MinTraceDuration { get; set; } = TimeSpan.Zero;
}
```

### Instrumentation Layer

```csharp
public static class SandboxTelemetry
{
    public static readonly string ServiceName = "AgentSandbox";
    public static readonly string Version = "1.0.0";
    
    // ActivitySource for distributed tracing
    public static readonly ActivitySource ActivitySource = 
        new(ServiceName, Version);
    
    // Meter for metrics
    public static readonly Meter Meter = 
        new(ServiceName, Version);
    
    // Instruments
    public static readonly Counter<long> CommandsExecuted;
    public static readonly Counter<long> CommandsFailed;
    public static readonly Histogram<double> CommandDuration;
    public static readonly Counter<long> FilesCreated;
    public static readonly Counter<long> FilesDeleted;
    public static readonly Counter<long> BytesWritten;
    public static readonly UpDownCounter<long> ActiveSandboxes;
    
    static SandboxTelemetry()
    {
        CommandsExecuted = Meter.CreateCounter<long>(
            "sandbox.commands.executed",
            description: "Number of commands executed");
            
        CommandsFailed = Meter.CreateCounter<long>(
            "sandbox.commands.failed",
            description: "Number of failed commands");
            
        CommandDuration = Meter.CreateHistogram<double>(
            "sandbox.commands.duration",
            unit: "ms",
            description: "Command execution duration");
            
        FilesCreated = Meter.CreateCounter<long>(
            "sandbox.files.created",
            description: "Number of files created");
            
        FilesDeleted = Meter.CreateCounter<long>(
            "sandbox.files.deleted",
            description: "Number of files deleted");
            
        BytesWritten = Meter.CreateCounter<long>(
            "sandbox.bytes.written",
            description: "Total bytes written to filesystem");
            
        ActiveSandboxes = Meter.CreateUpDownCounter<long>(
            "sandbox.active",
            description: "Number of active sandbox instances");
    }
}
```

## Metrics

### Sandbox Metrics

| Metric Name | Type | Unit | Description |
|-------------|------|------|-------------|
| `sandbox.active` | UpDownCounter | count | Active sandbox instances |
| `sandbox.created` | Counter | count | Total sandboxes created |
| `sandbox.disposed` | Counter | count | Total sandboxes disposed |
| `sandbox.lifetime` | Histogram | seconds | Sandbox lifetime duration |

### Command Metrics

| Metric Name | Type | Unit | Description |
|-------------|------|------|-------------|
| `sandbox.commands.executed` | Counter | count | Commands executed |
| `sandbox.commands.failed` | Counter | count | Commands with non-zero exit |
| `sandbox.commands.duration` | Histogram | ms | Execution duration |

**Dimensions/Tags:**
- `sandbox.id` - Sandbox identifier
- `command.name` - Command name (e.g., `ls`, `cat`, `git`)
- `command.exit_code` - Exit code (for failed commands)

### FileSystem Metrics

| Metric Name | Type | Unit | Description |
|-------------|------|------|-------------|
| `sandbox.files.created` | Counter | count | Files created |
| `sandbox.files.deleted` | Counter | count | Files deleted |
| `sandbox.files.modified` | Counter | count | Files modified |
| `sandbox.directories.created` | Counter | count | Directories created |
| `sandbox.bytes.written` | Counter | bytes | Total bytes written |
| `sandbox.bytes.read` | Counter | bytes | Total bytes read |
| `sandbox.storage.used` | Gauge | bytes | Current storage usage |
| `sandbox.storage.quota_pct` | Gauge | percent | Storage quota utilization |

### Skill Metrics

| Metric Name | Type | Unit | Description |
|-------------|------|------|-------------|
| `sandbox.skills.invoked` | Counter | count | Skill invocations |
| `sandbox.scripts.executed` | Counter | count | Shell scripts executed |
| `sandbox.scripts.failed` | Counter | count | Failed script executions |

## Traces

### Span Naming Convention

```
sandbox.{operation}
sandbox.command.{command_name}
sandbox.fs.{operation}
sandbox.skill.{skill_name}
```

### Command Execution Trace

```
sandbox.command.git [duration: 15ms]
├── Attributes:
│   ├── sandbox.id: "abc123"
│   ├── command.full: "git commit -m 'Initial'"
│   ├── command.name: "git"
│   ├── command.args: ["commit", "-m", "Initial"]
│   ├── command.exit_code: 0
│   ├── command.cwd: "/workspace"
│   └── command.output_length: 156
└── Events:
    └── command.completed [timestamp]
```

### FileSystem Operation Trace

```
sandbox.fs.write [duration: 2ms]
├── Attributes:
│   ├── sandbox.id: "abc123"
│   ├── fs.path: "/workspace/file.txt"
│   ├── fs.bytes: 1024
│   └── fs.operation: "write"
└── Events:
    └── file.written [timestamp]
```

### Skill Execution Trace

```
sandbox.skill.python-dev [duration: 150ms]
├── Attributes:
│   ├── sandbox.id: "abc123"
│   ├── skill.name: "python-dev"
│   └── skill.script: "scripts/setup.sh"
├── Children:
│   ├── sandbox.command.sh [50ms]
│   ├── sandbox.command.echo [5ms]
│   └── sandbox.command.mkdir [3ms]
└── Events:
    ├── skill.started [timestamp]
    └── skill.completed [timestamp]
```

## Structured Logging

### Log Schema

```csharp
public record SandboxLogEntry
{
    public DateTime Timestamp { get; init; }
    public string Level { get; init; }        // Info, Warning, Error
    public string SandboxId { get; init; }
    public string Category { get; init; }     // Command, FileSystem, Skill
    public string Message { get; init; }
    public Dictionary<string, object> Properties { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
}
```

### Log Categories

| Category | Events |
|----------|--------|
| `Sandbox` | Created, Disposed, Snapshot, Restore |
| `Command` | Executed, Failed, Timeout |
| `FileSystem` | Created, Modified, Deleted, Renamed, QuotaExceeded |
| `Skill` | Mounted, Invoked, ScriptExecuted, ScriptFailed |
| `Shell` | ExtensionRegistered, VariableSet |

### Example Log Entries

```json
{
  "timestamp": "2026-01-18T20:00:00.000Z",
  "level": "Info",
  "sandboxId": "abc123",
  "category": "Command",
  "message": "Command executed successfully",
  "properties": {
    "command": "git commit -m 'Initial'",
    "exitCode": 0,
    "durationMs": 15.2
  },
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "spanId": "00f067aa0ba902b7"
}
```

```json
{
  "timestamp": "2026-01-18T20:00:01.000Z",
  "level": "Warning",
  "sandboxId": "abc123",
  "category": "FileSystem",
  "message": "Storage quota 80% utilized",
  "properties": {
    "usedBytes": 8388608,
    "maxBytes": 10485760,
    "utilizationPct": 80.0
  }
}
```

## Real-time Event Streaming

### Event Types

```csharp
public abstract record SandboxEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required string SandboxId { get; init; }
    public string? TraceId { get; init; }
}

public record CommandExecutedEvent : SandboxEvent
{
    public required string Command { get; init; }
    public required int ExitCode { get; init; }
    public required TimeSpan Duration { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
}

public record FileChangedEvent : SandboxEvent
{
    public required string Path { get; init; }
    public required FileChangeType ChangeType { get; init; }
    public long? BytesDelta { get; init; }
}

public enum FileChangeType { Created, Modified, Deleted, Renamed }

public record SkillInvokedEvent : SandboxEvent
{
    public required string SkillName { get; init; }
    public string? ScriptPath { get; init; }
}

public record SandboxErrorEvent : SandboxEvent
{
    public required string Category { get; init; }
    public required string Message { get; init; }
    public string? StackTrace { get; init; }
}
```

## Observer Pattern Implementation

The `Sandbox` class implements `IObservableSandbox` for real-time event streaming:

```csharp
public interface IObservableSandbox
{
    IDisposable Subscribe(ISandboxObserver observer);
}

public interface ISandboxObserver
{
    void OnCommandExecuted(CommandExecutedEvent e);
    void OnFileChanged(FileChangedEvent e);
    void OnSkillInvoked(SkillInvokedEvent e);
    void OnLifecycleEvent(SandboxLifecycleEvent e);
    void OnError(SandboxErrorEvent e);
}
```

### Custom Observer Example

```csharp
// Using delegate observer
var observer = new DelegateSandboxObserver
{
    OnCommandExecutedHandler = e => Console.WriteLine($"Command: {e.Command}"),
    OnErrorHandler = e => Console.WriteLine($"Error: {e.Message}")
};
using var subscription = sandbox.Subscribe(observer);

// Using base class
public class MyObserver : SandboxObserverBase
{
    public override void OnCommandExecuted(CommandExecutedEvent e)
    {
        if (e.ExitCode != 0)
            _logger.LogWarning("Command failed: {Command}", e.Command);
    }
}
```

### FileSystem Events

The `FileSystem` class raises events for file operations:

```csharp
public event EventHandler<FileSystemEventArgs>? Created;
public event EventHandler<FileSystemEventArgs>? Changed;
public event EventHandler<FileSystemEventArgs>? Deleted;
public event EventHandler<FileSystemRenamedEventArgs>? Renamed;
```

Events are raised outside locks to prevent deadlocks.

## OpenTelemetry Integration

### Configuration

```csharp
public static class SandboxTelemetryExtensions
{
    /// <summary>
    /// Adds AgentSandbox instrumentation to OpenTelemetry.
    /// </summary>
    public static TracerProviderBuilder AddSandboxInstrumentation(
        this TracerProviderBuilder builder)
    {
        return builder.AddSource(SandboxTelemetry.ServiceName);
    }
    
    public static MeterProviderBuilder AddSandboxInstrumentation(
        this MeterProviderBuilder builder)
    {
        return builder.AddMeter(SandboxTelemetry.ServiceName);
    }
}
```

### Usage Example

```csharp
// Program.cs or Startup.cs
var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyApp"))
    .AddSandboxInstrumentation()
    .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317"))
    .Build();

var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyApp"))
    .AddSandboxInstrumentation()
    .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317"))
    .Build();

// Create sandbox with telemetry enabled
var options = new SandboxOptions
{
    Telemetry = new SandboxTelemetryOptions
    {
        Enabled = true,
        TraceCommands = true,
        TraceFileSystem = true,
        CollectMetrics = true
    }
};

var sandbox = new Sandbox(options: options);
```

### OTLP Export Targets

Compatible with any OTLP receiver:

| Backend | Description |
|---------|-------------|
| Jaeger | Distributed tracing |
| Zipkin | Distributed tracing |
| Prometheus | Metrics collection |
| Grafana Tempo | Traces |
| Grafana Loki | Logs |
| Azure Monitor | Full observability |
| AWS X-Ray | AWS tracing |
| Datadog | Full observability |
| Honeycomb | Observability platform |
| Seq | Structured logs |

## Implementation Status

### Phase 1: Foundation ✅
- [x] Created `AgentSandbox.Core/Telemetry/` namespace
- [x] Implemented `SandboxTelemetry` static class with ActivitySource and Meter
- [x] Implemented `SandboxTelemetryOptions` configuration
- [x] Added telemetry option to `SandboxOptions`

### Phase 2: Metrics ✅
- [x] Implemented command execution metrics in `Sandbox.Execute()`
- [x] Implemented filesystem events in `FileSystem.cs`
- [x] Implemented sandbox lifecycle metrics (create/dispose)
- [x] Added metric tags/dimensions with instance ID for distributed systems

### Phase 3: Tracing ✅
- [x] Added sandbox-level activity (spans entire lifecycle)
- [x] Added command execution spans (children of sandbox activity)
- [x] Implemented trace context propagation via Activity.Current

### Phase 4: Events & Logging ✅
- [x] Implemented `ISandboxObserver` interface and base classes
- [x] Wired up FileSystem events (Created, Changed, Deleted, Renamed)
- [x] Implemented observer pattern with thread-safe subscription

### Phase 5: Application Insights Integration ✅
- [x] Created `ApplicationInsightsObserver` in AgentSandbox.Extensions
- [x] Created `ApplicationInsightsExtensions` for fluent API
- [x] Configurable tracking (commands, files, skills, lifecycle)
- [x] Error tracking with exceptions

## Application Insights Usage

```csharp
using AgentSandbox.Core;
using AgentSandbox.Extensions;
using Microsoft.ApplicationInsights;

// Create sandbox with telemetry enabled
var options = new SandboxOptions
{
    Telemetry = new SandboxTelemetryOptions { Enabled = true }
};
var sandbox = new Sandbox(options: options);

// Add Application Insights observer
var telemetryClient = new TelemetryClient(configuration);
using var subscription = sandbox.AddApplicationInsights(telemetryClient);

// Or with custom options
using var subscription = sandbox.AddApplicationInsights(telemetryClient, opts =>
{
    opts.TrackCommands = true;
    opts.TrackFileChanges = false;  // Reduce noise
    opts.TrackSkills = true;
    opts.TrackLifecycle = true;
    opts.RedactCommandOutput = true;  // Security
    opts.MaxOutputLength = 500;
});

// Execute commands - automatically tracked
sandbox.Execute("git init");
sandbox.Execute("echo 'Hello'");
```

### ApplicationInsightsObserverOptions

| Option | Default | Description |
|--------|---------|-------------|
| `TrackCommands` | `true` | Track command executions as events and dependencies |
| `TrackFileChanges` | `true` | Track file system changes as events |
| `TrackSkills` | `true` | Track skill invocations as events |
| `TrackLifecycle` | `true` | Track sandbox create/dispose events |
| `RedactCommandOutput` | `false` | Replace output with `[REDACTED]` |
| `MaxOutputLength` | `1000` | Truncate output in tracked events |

## Performance Considerations

| Concern | Mitigation |
|---------|------------|
| **Disabled overhead** | Check `Enabled` flag before any instrumentation |
| **String allocations** | Use `ActivitySource.HasListeners()` before creating spans |
| **High-frequency ops** | Sample filesystem events, batch metrics |
| **Memory pressure** | Limit event buffer sizes, use object pooling |

```csharp
// Example: Zero-cost when disabled
public ShellResult Execute(string command)
{
    Activity? activity = null;
    
    if (_options.Telemetry?.Enabled == true && 
        SandboxTelemetry.ActivitySource.HasListeners())
    {
        activity = SandboxTelemetry.ActivitySource.StartActivity(
            $"sandbox.command.{GetCommandName(command)}");
        activity?.SetTag("sandbox.id", Id);
        activity?.SetTag("command.full", command);
    }
    
    try
    {
        var result = _shell.Execute(command);
        
        activity?.SetTag("command.exit_code", result.ExitCode);
        
        if (_options.Telemetry?.CollectMetrics == true)
        {
            SandboxTelemetry.CommandsExecuted.Add(1,
                new KeyValuePair<string, object?>("sandbox.id", Id),
                new KeyValuePair<string, object?>("command.name", GetCommandName(command)));
        }
        
        return result;
    }
    finally
    {
        activity?.Dispose();
    }
}
```

## Security Considerations

| Concern | Mitigation |
|---------|------------|
| **Sensitive data in logs** | Redact file contents, limit output capture |
| **Command injection in traces** | Sanitize command strings |
| **PII in paths** | Option to hash or mask path segments |
| **High cardinality** | Limit unique tag values |

## References

- [OpenTelemetry .NET SDK](https://opentelemetry.io/docs/instrumentation/net/)
- [OpenTelemetry Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/)
- [System.Diagnostics.ActivitySource](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs)
