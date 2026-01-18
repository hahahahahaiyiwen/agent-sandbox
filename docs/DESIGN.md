# Agent Sandbox

## Overview

Agent Sandbox is an in-memory isolated execution environment designed for AI agents. It provides a virtual filesystem and Unix-like shell that agents can use to read, write, and manipulate files without affecting the host system.

## Goals

- **Complete Isolation** - Agents operate in a sandboxed environment with no access to the host filesystem
- **Familiar Interface** - Unix-like shell commands and POSIX-style paths that AI agents understand
- **Snapshotting** - Save and restore sandbox state for checkpointing and rollback
- **Resource Limits** - Enforce quotas on file sizes, total storage, and node counts
- **Extensibility** - Pluggable storage backends and customizable shell commands

---

## Usage

### As a Library

Embed the sandbox directly in your application to provide agents with an isolated environment.

```
┌─────────────────────────────────────────────────────────────┐
│                    Your Application                          │
│                                                              │
│   ┌──────────────┐                                          │
│   │    Agent     │                                          │
│   │   (LLM/AI)   │                                          │
│   └──────┬───────┘                                          │
│          │                                                   │
│          ▼                                                   │
│   ┌──────────────────────────────────────────────────────┐  │
│   │              AgentSandboxInstance                     │  │
│   │                                                       │  │
│   │   Execute("ls -la")         → ShellResult            │  │
│   │   Execute("cat file.txt")   → ShellResult            │  │
│   │   Execute("echo 'x' > f")   → ShellResult            │  │
│   │   CreateSnapshot()          → SandboxSnapshot        │  │
│   │   RestoreSnapshot(data)                              │  │
│   │   GetStats()                → SandboxStats           │  │
│   │                                                       │  │
│   └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

**Workflow**:
1. Create an `Sandbox` with configuration options
2. Agent calls `Execute(command)` to run shell commands
3. Process results (stdout, stderr, exit code)
4. Use `CreateSnapshot()` before risky operations
5. Use `RestoreSnapshot()` to rollback on failure
6. Dispose sandbox when session ends

**Use Cases**:
- Code generation agents that need to write and test files
- Data processing agents that manipulate files
- Autonomous agents needing a persistent working environment
- Testing environments for agent behavior

---

### With Semantic Kernel

The `AgentSandbox.Extensions` package provides seamless integration with Microsoft Semantic Kernel.

```csharp
using Microsoft.SemanticKernel;
using AgentSandbox.Extensions.SemanticKernel;

// Register sandbox services with the kernel builder
var builder = Kernel.CreateBuilder();
builder.AddSandboxManager(new SandboxOptions
{
    MaxTotalSize = 100 * 1024 * 1024,  // 100 MB
    MaxFileSize = 10 * 1024 * 1024      // 10 MB
});

var kernel = builder.Build();

// Get the sandbox function for agent tool calling
var sandboxFunction = kernel.GetSandboxFunction();

// Add to a plugin for agent use
kernel.ImportPluginFromFunctions("Sandbox", [sandboxFunction]);
```

**Extension Methods**:
| Method | Description |
|--------|-------------|
| `AddSandboxManager(options?)` | Registers `SandboxManager` as singleton and `Sandbox` as scoped service |
| `GetSandboxFunction(options?)` | Creates a `KernelFunction` for command execution |
| `CreateSandboxFunction(sandbox, options?)` | Static method to create a function from a `Sandbox` instance |

**Built-in Commands** (exposed via the function):
`ls`, `cat`, `mkdir`, `rm`, `cp`, `mv`, `echo`, `grep`, `find`, `head`, `tail`, `wc`, `pwd`, `cd`, `touch`, `env`, `export`

**Use Cases**:
- AI agents with tool-calling capabilities
- Semantic Kernel-based autonomous agents
- Multi-turn agent conversations with file persistence

---

### As a Server

Deploy as a multi-tenant service where multiple agents have isolated sandboxes.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           Sandbox Server                                 │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                        REST API Layer                               │ │
│  │                                                                     │ │
│  │  POST   /sandboxes                - Create sandbox                  │ │
│  │  GET    /sandboxes                - List all sandboxes              │ │
│  │  GET    /sandboxes/{id}           - Get sandbox info                │ │
│  │  DELETE /sandboxes/{id}           - Destroy sandbox                 │ │
│  │  POST   /sandboxes/{id}/execute   - Execute command                 │ │
│  │  GET    /sandboxes/{id}/history   - Get command history             │ │
│  │  POST   /sandboxes/{id}/snapshot  - Create snapshot                 │ │
│  │  POST   /sandboxes/{id}/restore   - Restore from snapshot           │ │
│  │  GET    /sandboxes/{id}/stats     - Get statistics                  │ │
│  │                                                                     │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                   │                                      │
│                                   ▼                                      │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                       SandboxManager                                │ │
│  │                                                                     │ │
│  │  • Create / Get / Destroy sandbox instances                        │ │
│  │  • Track active sandboxes by ID                                    │ │
│  │  • Cleanup inactive sandboxes                                      │ │
│  │  • Aggregate statistics                                            │ │
│  │                                                                     │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                   │                                      │
│            ┌──────────────────────┼──────────────────────┐              │
│            ▼                      ▼                      ▼              │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐      │
│  │ Sandbox: agent-1 │  │ Sandbox: agent-2 │  │ Sandbox: agent-3 │      │
│  │   (isolated)     │  │   (isolated)     │  │   (isolated)     │      │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘      │
└─────────────────────────────────────────────────────────────────────────┘
```

**SandboxManager API**:
| Method | Description |
|--------|-------------|
| Create(id?, options?) | Create new sandbox instance |
| Get(id) | Get existing sandbox by ID |
| GetOrCreate(id) | Get or create sandbox |
| Destroy(id) | Dispose sandbox and release resources |
| List() | List all active sandbox IDs |
| GetAllStats() | Get statistics for all sandboxes |
| CleanupInactive() | Remove sandboxes idle beyond timeout |

**Use Cases**:
- Multi-agent platforms where each agent needs isolation
- Agent-as-a-service offerings
- Development environments for testing agent code
- Sandboxed code execution services

---

## Configuration

### SandboxOptions

| Option | Default | Description |
|--------|---------|-------------|
| MaxTotalSize | 100 MB | Maximum total storage size |
| MaxFileSize | 10 MB | Maximum single file size |
| MaxNodeCount | 10,000 | Maximum files + directories |
| CommandTimeout | 30 sec | Maximum command execution time |
| WorkingDirectory | `/` | Initial working directory |
| Environment | empty | Initial environment variables |

---

## Project Structure

```
AgentSandbox/
├── AgentSandbox.Core/           # Core library
│   ├── FileSystem/              # Virtual filesystem
│   │   ├── IFileSystem.cs       # Filesystem interfaces
│   │   ├── IFileStorage.cs      # Storage abstraction
│   │   ├── FileEntry.cs         # File/directory data class
│   │   ├── FileSystem.cs        # Main implementation
│   │   └── Storage/
│   │       └── InMemoryFileStorage.cs
│   ├── Shell/                   # Shell emulator
│   │   ├── ISandboxShell.cs     # Shell interface
│   │   ├── IShellCommand.cs     # Extension interface
│   │   ├── IShellContext.cs     # Context for extensions
│   │   ├── SandboxShell.cs      # Command processor
│   │   ├── ShellResult.cs       # Command result
│   │   └── Extensions/
│   │       └── CurlCommand.cs   # HTTP client command
│   └── Sandbox/                 # Sandbox management
│       ├── AgentSandboxInstance.cs
│       └── SandboxManager.cs
├── AgentSandbox.Extensions/ # Semantic Kernel integration
│   └── KernelExtensions.cs      # Extension methods for IKernelBuilder
├── AgentSandbox.Api/            # REST API server
│   ├── Program.cs
│   └── Endpoints/
│       └── SandboxEndpoints.cs
├── AgentSandbox.Tests/          # Unit tests
└── docs/
    ├── DESIGN.md                # This document (high-level)
    ├── SANDBOX_DESIGN.md        # Sandbox instance internals
    ├── FILESYSTEM_DESIGN.md     # Filesystem design
    ├── SHELL_EXTENSIONS.md      # Shell extension system
    └── OBSERVABILITY.md         # Monitoring and telemetry
```

---

## Related Documentation

- [SANDBOX_DESIGN.md](./SANDBOX_DESIGN.md) - Internal design of Sandbox Instance, Shell, and Shell Extensions
- [FILESYSTEM_DESIGN.md](./FILESYSTEM_DESIGN.md) - FileSystem interfaces and storage abstraction
- [SHELL_EXTENSIONS.md](./SHELL_EXTENSIONS.md) - Shell extension architecture, built-in commands, and roadmap
- [AGENT_SKILLS.md](./AGENT_SKILLS.md) - Agent Skills integration for structured skill packages
- [OBSERVABILITY.md](./OBSERVABILITY.md) - Production monitoring, OpenTelemetry integration, metrics and tracing
