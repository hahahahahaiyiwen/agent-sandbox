# Shell Extensions Design

This document describes the shell extension system for AgentSandbox, including the current implementation and future roadmap.

## Overview

AgentSandbox provides a sandboxed shell environment with built-in commands and an extensible architecture for adding custom commands. Extensions allow AI agents to interact with external services while maintaining sandbox isolation.

## Architecture

### Extension Interface

```csharp
public interface IShellCommand
{
    string Name { get; }                           // Primary command name (e.g., "curl")
    IReadOnlyList<string> Aliases { get; }         // Alternative names
    string Description { get; }                    // Short help text
    string Usage { get; }                          // Usage examples
    ShellResult Execute(string[] args, IShellContext context);
}
```

### Shell Context

Extensions receive an `IShellContext` providing:

| Property | Description |
|----------|-------------|
| `FileSystem` | Virtual filesystem for reading/writing files |
| `CurrentDirectory` | Current working directory |
| `Environment` | Environment variables dictionary |
| `ResolvePath(path)` | Resolves relative paths to absolute |

### Registration

Extensions are registered via `SandboxOptions`:

```csharp
var options = new SandboxOptions
{
    ShellExtensions = new IShellCommand[]
    {
        new CurlCommand(),
        new MyCustomCommand()
    }
};

var sandbox = new Sandbox(options: options);
```

---

## Built-in Commands

These commands are always available in the sandbox shell:

| Command | Description |
|---------|-------------|
| `pwd` | Print working directory |
| `cd` | Change directory |
| `ls` | List directory contents |
| `cat` | Display file contents |
| `echo` | Print text (supports `>` and `>>` redirection) |
| `mkdir` | Create directories (`-p` for parents) |
| `rm` | Remove files/directories (`-r` for recursive) |
| `cp` | Copy files/directories |
| `mv` | Move/rename files |
| `touch` | Create empty file or update timestamp |
| `head` | Display first lines of file |
| `tail` | Display last lines of file |
| `wc` | Count lines, words, characters |
| `grep` | Search file contents |
| `find` | Find files by name pattern |
| `env` | Display environment variables |
| `export` | Set environment variable |
| `clear` | Clear screen (no-op in non-interactive mode) |
| `help` | Display available commands |

---

## Current Extensions

### CurlCommand

HTTP client for making web requests from the sandbox.

**Location:** `AgentSandbox.Core/ShellExtensions/CurlCommand.cs`

**Usage:**
```bash
curl [options] <url>
```

**Options:**

| Option | Description |
|--------|-------------|
| `-X, --request <method>` | HTTP method (GET, POST, PUT, DELETE, PATCH) |
| `-H, --header <header>` | Add header (repeatable) |
| `-d, --data <data>` | Request body data |
| `-o, --output <file>` | Write output to file |
| `-s, --silent` | Silent mode |
| `-i, --include` | Include response headers |
| `-L, --location` | Follow redirects |

**Examples:**
```bash
# GET request
curl https://api.example.com/data

# POST with JSON
curl -X POST -H "Content-Type: application/json" -d '{"key":"value"}' https://api.example.com/data

# Save response to file
curl -o response.json https://api.example.com/data
```

**Notes:**
- Requires injecting `HttpClient` for testability
- Blocks on async operations (runs synchronously in shell)
- Respects sandbox filesystem for `-o` output

---

## Planned Extensions

### High Priority

#### 1. `jq` - JSON Processor
Parse, filter, and transform JSON data.

```bash
# Extract field
echo '{"name":"test"}' | jq '.name'

# Filter array
cat data.json | jq '.items[] | select(.active == true)'
```

**Use cases:** API response parsing, configuration manipulation, data transformation.

#### 2. `tar` / `zip` - Archive Commands
Create and extract archives within the virtual filesystem.

```bash
tar -czf archive.tar.gz ./src
tar -xzf archive.tar.gz
zip -r backup.zip ./project
unzip backup.zip
```

**Use cases:** Project packaging, backup/restore, multi-file transfers.

#### 3. `sed` / `awk` - Text Processing
Stream editing and pattern-based text processing.

```bash
sed 's/old/new/g' file.txt
awk '{print $1, $3}' data.csv
```

**Use cases:** File transformations, log processing, data extraction.

#### 4. `diff` / `patch` - File Comparison
Compare files and apply patches.

```bash
diff file1.txt file2.txt
diff -u old.txt new.txt > changes.patch
patch < changes.patch
```

**Use cases:** Code review, change tracking, merge operations.

### Medium Priority

#### 5. `git` - Version Control (Simulated)
Basic git operations within the virtual filesystem.

```bash
git init
git add .
git commit -m "message"
git log
git diff
git status
```

**Notes:** Would simulate git operations without actual git repository. Useful for teaching agents version control concepts.

#### 6. `sqlite` - Database Operations
In-memory SQLite database for structured data.

```bash
sqlite mydb.db "CREATE TABLE users (id INT, name TEXT)"
sqlite mydb.db "INSERT INTO users VALUES (1, 'Alice')"
sqlite mydb.db "SELECT * FROM users"
```

**Use cases:** Data storage, structured queries, application prototyping.

#### 7. `base64` - Encoding/Decoding
Base64 encoding and decoding.

```bash
echo "Hello" | base64
echo "SGVsbG8=" | base64 -d
```

**Use cases:** Binary data handling, API authentication, data embedding.

#### 8. `xxd` / `hexdump` - Binary Inspection
View and manipulate binary data.

```bash
xxd file.bin
xxd -r hex.txt > file.bin
```

**Use cases:** Binary file inspection, debugging, data format analysis.

### Lower Priority / Specialized

#### 9. `python` - Python Interpreter (Sandboxed)
Execute Python code within constraints.

```bash
python script.py
python -c "print(2 + 2)"
```

**Notes:** Would require careful sandboxing. Could use Roslyn for C# scripting as alternative.

#### 10. `node` - JavaScript Runtime (Sandboxed)
Execute JavaScript code.

```bash
node script.js
node -e "console.log('Hello')"
```

#### 11. `dotnet` - .NET CLI (Simulated)
Simulated dotnet operations for project scaffolding.

```bash
dotnet new console -n MyApp
dotnet build
dotnet run
```

#### 12. `make` / `task` - Build Automation
Task runner for defined workflows.

```bash
# Makefile or Taskfile.yaml support
make build
task test
```

#### 13. `cron` / `at` - Scheduled Tasks
Schedule command execution (within sandbox session lifetime).

```bash
at now + 5 minutes -c "echo 'reminder' > note.txt"
```

---

## Extension Development Guide

### Creating a New Extension

1. Implement `IShellCommand`:

```csharp
public class MyCommand : IShellCommand
{
    public string Name => "mycmd";
    public IReadOnlyList<string> Aliases => new[] { "mc" };
    public string Description => "Does something useful";
    public string Usage => "mycmd [options] <args>";

    public ShellResult Execute(string[] args, IShellContext context)
    {
        // Parse arguments
        // Perform operation using context.FileSystem
        // Return result
        return ShellResult.Ok("Output");
    }
}
```

2. Register the extension:

```csharp
var options = new SandboxOptions
{
    ShellExtensions = new[] { new MyCommand() }
};
```

### Best Practices

1. **Argument Parsing:** Support both short (`-o`) and long (`--output`) options
2. **Error Handling:** Return `ShellResult.Error()` with descriptive messages
3. **Help Text:** Provide clear `Usage` string with examples
4. **Filesystem Isolation:** Only use `context.FileSystem`, never access real filesystem
5. **Async Operations:** Block on async calls since shell is synchronous
6. **Testability:** Accept dependencies via constructor injection
7. **Idempotency:** Commands should be safe to re-run where possible

### Testing Extensions

```csharp
[Fact]
public void MyCommand_ShouldDoSomething()
{
    var fs = new FileSystem();
    var shell = new SandboxShell(fs);
    shell.RegisterCommand(new MyCommand());
    
    var result = shell.Execute("mycmd --option value");
    
    Assert.True(result.Success);
    Assert.Contains("expected output", result.Stdout);
}
```

---

## Security Considerations

- **Network Access:** Extensions like `curl` provide controlled network access. Consider restricting URLs or requiring allowlists for production use.
- **Resource Limits:** Extensions should respect sandbox limits (file size, node count, execution time).
- **External Dependencies:** Extensions requiring external services should handle failures gracefully.
- **Code Execution:** Script interpreters (python, node) require additional sandboxing layers.

---

## Future Considerations

1. **Async Command Support:** Allow commands to yield during long operations
2. **Pipeline Improvements:** Better stdin/stdout piping between commands
3. **Command Chaining:** Support `&&`, `||`, `;` operators
4. **Background Jobs:** Support `&` for background execution
5. **Plugin Discovery:** Auto-discover extensions via assembly scanning
6. **Permission System:** Fine-grained permissions per extension
