# AgentSandbox Roadmap

This document outlines planned features and improvements for AgentSandbox, sorted by priority.

## Current State (v1.0)

### Completed Features ✅

| Category | Feature | Status |
|----------|---------|--------|
| **Core** | In-memory virtual filesystem | ✅ |
| **Core** | Sandboxed shell with built-in commands | ✅ |
| **Core** | Snapshot and restore | ✅ |
| **Core** | Storage quotas (file size, total size) | ✅ |
| **Shell** | Built-in commands (ls, cat, mkdir, rm, cp, mv, echo, grep, find, head, tail, wc, pwd, cd, touch, env, export, sh) | ✅ |
| **Shell** | Output redirection (>, >>) | ✅ |
| **Shell** | Shell script execution (.sh files) | ✅ |
| **Extensions** | curl - HTTP client | ✅ |
| **Extensions** | jq - JSON processor | ✅ |
| **Extensions** | git - Simulated version control | ✅ |
| **Skills** | Agent Skills mounting and discovery | ✅ |
| **Skills** | SKILL.md manifest parsing | ✅ |
| **Observability** | OpenTelemetry metrics and tracing | ✅ |
| **Observability** | Observer pattern for real-time events | ✅ |
| **Observability** | Application Insights integration | ✅ |
| **Integration** | Semantic Kernel extensions | ✅ |
| **Integration** | Dependency injection extensions | ✅ |

---

## Priority 1: Critical Path

### 1.1 Pipeline Support (Low Priority - Documented Limitation)

**Status:** Deferred with helpful error message

Pipelines (`cmd1 | cmd2`) are intentionally not supported. The shell now returns a helpful error message with workarounds when a pipeline is detected.

**Rationale:** AI agents typically process command outputs programmatically rather than chaining shell commands. The added complexity doesn't justify the benefit.

---

### 1.2 Stdin Support for Commands

**Effort:** Medium | **Impact:** Medium

Add stdin support to enable reading from input streams, primarily for script execution.

```csharp
public interface IShellContext
{
    // Existing
    IFileSystem FileSystem { get; }
    string CurrentDirectory { get; }
    
    // New
    TextReader? StandardInput { get; }
}
```

**Use cases:**
- Interactive script prompts
- Here-documents in shell scripts
- Data piping within scripts

---

## Priority 2: High Value Extensions

### 2.1 Archive Commands (tar/zip)

**Effort:** Medium | **Impact:** High

Create and extract archives within the virtual filesystem.

```bash
tar -czf archive.tar.gz ./src
tar -xzf archive.tar.gz -C ./output
zip -r backup.zip ./project
unzip backup.zip -d ./extracted
```

**Use cases:**
- Project packaging and distribution
- Backup/restore operations
- Multi-file transfers

---

### 2.2 Text Processing (sed)

**Effort:** Medium | **Impact:** High

Stream editor for text transformations.

```bash
sed 's/old/new/g' file.txt
sed -i 's/TODO/DONE/' file.txt
sed '/pattern/d' file.txt
```

**Implementation:**
- Support common substitution patterns
- Support line deletion/insertion
- Support in-place editing (-i flag)

---

### 2.3 File Comparison (diff)

**Effort:** Medium | **Impact:** High

Compare files and generate patches.

```bash
diff file1.txt file2.txt
diff -u old.txt new.txt > changes.patch
```

**Use cases:**
- Code review and change tracking
- Configuration comparison
- Merge conflict visualization

---

## Priority 3: Enhanced Observability

### 3.1 Structured Logging Integration

**Effort:** Low | **Impact:** Medium

Add ILogger integration for structured logging.

```csharp
var sandbox = new Sandbox(options: new SandboxOptions
{
    Telemetry = new SandboxTelemetryOptions
    {
        Enabled = true,
        Logger = loggerFactory.CreateLogger<Sandbox>()
    }
});
```

---

### 3.2 Metrics Dashboard Templates

**Effort:** Low | **Impact:** Medium

Provide Grafana/Azure Dashboard templates for sandbox monitoring.

**Dashboards:**
- Command execution rates and durations
- Error rates by command type
- Storage utilization over time
- Active sandbox count

---

### 3.3 OpenTelemetry Collector Integration

**Effort:** Low | **Impact:** Medium

Create `AgentSandbox.Extensions.OpenTelemetry` package with TracerProvider and MeterProvider extensions.

```csharp
services.AddOpenTelemetry()
    .WithTracing(b => b.AddSandboxInstrumentation())
    .WithMetrics(b => b.AddSandboxInstrumentation());
```

---

## Priority 4: Developer Experience

### 4.1 REPL Mode for Playground

**Effort:** Low | **Impact:** Medium

Enhanced interactive mode with features:
- Command history (up/down arrows)
- Tab completion for commands and paths
- Colored output
- Multi-line input for scripts

---

### 4.2 Sandbox Debugger

**Effort:** Medium | **Impact:** Medium

Step-through debugging for shell scripts.

```bash
sh -x script.sh  # Print each command before execution
sh -v script.sh  # Verbose mode with line numbers
```

---

### 4.3 Extension Generator Template

**Effort:** Low | **Impact:** Low

`dotnet new` template for creating shell extensions.

```bash
dotnet new agentsandbox-extension -n MyCommand
```

---

## Priority 5: Advanced Extensions

### 5.1 SQLite Database

**Effort:** High | **Impact:** Medium

In-memory SQLite database for structured data operations.

```bash
sqlite mydb.db "CREATE TABLE users (id INT, name TEXT)"
sqlite mydb.db "SELECT * FROM users WHERE id = 1"
```

**Implementation options:**
- Use Microsoft.Data.Sqlite with in-memory database
- Store database files in virtual filesystem

---

### 5.2 Base64 Encoding

**Effort:** Low | **Impact:** Low

Base64 encoding and decoding.

```bash
base64 -e file.bin > encoded.txt
base64 -d encoded.txt > file.bin
echo "Hello" | base64
```

---

### 5.3 Hex Dump (xxd)

**Effort:** Low | **Impact:** Low

Binary file inspection.

```bash
xxd file.bin
xxd -r hex.txt > file.bin
```

---

## Priority 6: Advanced Features (Future)

### 6.1 Sandboxed Script Runtimes

**Effort:** Very High | **Impact:** High

Execute code in sandboxed interpreters.

| Runtime | Approach |
|---------|----------|
| Python | Embedded IronPython or subprocess with restrictions |
| JavaScript | Jint interpreter |
| C# | Roslyn scripting with sandboxed AppDomain |

**Considerations:**
- Security isolation is critical
- Resource limits (CPU, memory, time)
- Filesystem access through virtual FS only

---

### 6.2 Network Simulation

**Effort:** High | **Impact:** Medium

Simulate network services within the sandbox.

```bash
# Start a mock HTTP server
mockserver --port 8080 --responses ./mocks

# Test against it
curl http://localhost:8080/api/users
```

---

### 6.3 Process Simulation

**Effort:** High | **Impact:** Low

Simulated background processes and job control.

```bash
sleep 10 &
jobs
fg %1
kill %1
```

---

## Non-Goals

The following are explicitly out of scope:

| Feature | Reason |
|---------|--------|
| Real filesystem access | Violates sandbox isolation |
| Network access (except curl) | Security risk; curl provides controlled access |
| Process spawning | No real OS process isolation |
| Full POSIX compliance | Diminishing returns; focus on agent use cases |
| Interactive TTY emulation | Agents don't need terminal UI |

---

## Version Milestones

### v1.1 (Next)
- [ ] sed command
- [ ] diff command
- [ ] Structured logging integration
- [ ] OpenTelemetry package

### v1.2
- [ ] tar/zip commands
- [ ] base64 command
- [ ] Dashboard templates
- [ ] Extension generator template

### v2.0 (Future)
- [ ] SQLite extension
- [ ] Stdin support
- [ ] Sandboxed script runtime (TBD which language)

---

## Contributing

To propose new features:
1. Open an issue describing the use case
2. Discuss implementation approach
3. Submit PR with tests and documentation

Priority is determined by:
1. **Agent utility** - How useful is this for AI agent workflows?
2. **Implementation effort** - Complexity vs. value
3. **Security** - Does it maintain sandbox isolation?
4. **Maintainability** - Long-term maintenance burden
