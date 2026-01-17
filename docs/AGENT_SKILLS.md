# Agent Skills Integration

This document describes the integration of [Agent Skills](https://agentskills.io/specification) into AgentSandbox, enabling AI agents to discover and use structured skill packages for improved accuracy and efficiency.

## Overview

Agent Skills are folders containing instructions, scripts, and resources that agents can use to perform tasks more effectively. AgentSandbox integrates skills by:

1. **Mounting** skill folders into the virtual filesystem at `/.sandbox/skills/{name}/`
2. **Exposing** skill discovery via the `GetSkill` tool description (available skills listed in description)
3. **Executing** skill scripts through the regular sandbox shell (`sh` command)

```
┌─────────────────────────────────────────────────────────────────┐
│                        Agent Workflow                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  1. Agent sees GetSkill tool → description lists available skills│
│  2. Agent calls GetSkill("python-dev") → reads instructions      │
│  3. Agent follows instructions using sandbox Execute()           │
│  4. Agent runs skill scripts via: sh /.sandbox/skills/name/...  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Skill Structure

Skills follow the [agentskills.io specification](https://agentskills.io/specification):

```
my-skill/
├── SKILL.md          # Required: instructions + metadata (frontmatter)
├── scripts/          # Optional: executable shell scripts
├── references/       # Optional: documentation and references
└── assets/           # Optional: templates, resources
```

### SKILL.md Format

The `SKILL.md` file is required and must contain YAML frontmatter with `name` and `description`:

```markdown
---
name: pdf-processing
description: Extract text and tables from PDF files, fill forms, merge documents.
---

# PDF Processing

## When to use this skill
Use this skill when the user needs to work with PDF files...

## How to extract text
1. Use pdfplumber for text extraction...

## How to fill forms
...
```

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Semantic Kernel Layer                        │
│                    ┌─────────────┐                              │
│                    │  GetSkill   │                              │
│                    │ (desc has   │                              │
│                    │ skill list) │                              │
│                    └──────┬──────┘                              │
│                           │                                      │
├───────────────────────────┴─────────────────────────────────────┤
│                        Sandbox Layer                             │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Sandbox                                                  │   │
│  │  ├── GetMountedSkills() → List<SkillInfo>                │   │
│  │  ├── Execute("sh /.sandbox/skills/name/script.sh")       │   │
│  │  └── Skills mounted at init from SandboxOptions          │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
├──────────────────────────────┴──────────────────────────────────┤
│                      Virtual Filesystem                          │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  /.sandbox/skills/                                        │   │
│  │  ├── pdf-processing/                                      │   │
│  │  │   ├── SKILL.md                                         │   │
│  │  │   ├── scripts/                                         │   │
│  │  │   │   └── extract.sh                                   │   │
│  │  │   ├── references/                                      │   │
│  │  │   └── assets/                                          │   │
│  │  └── git-workflow/                                        │   │
│  │       ├── SKILL.md                                        │   │
│  │       └── scripts/                                        │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
├──────────────────────────────┴──────────────────────────────────┤
│                        Shell Layer                               │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  sh command / .sh interpreter                             │   │
│  │  └── Executes shell scripts line-by-line                  │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

## Skill Source Abstraction

Skills can be loaded from multiple sources to support different deployment scenarios:

```
┌─────────────────────────────────────────────────────────────────┐
│                    ISkillSource Interface                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ISkillSource.GetFiles() → IEnumerable<SkillFile>               │
│                                                                  │
│  Implementations:                                                │
│  ├── FileSystemSkillSource    (local folder - development)      │
│  ├── EmbeddedSkillSource      (assembly resources - production) │
│  └── InMemorySkillSource      (programmatic - testing)          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Core Models

### AgentSkill

Factory class for creating skills from various sources:

```csharp
// From local filesystem (development, CLI tools)
var skill = AgentSkill.FromPath("C:/skills/pdf-processing");

// From embedded assembly resources (production, NuGet packages)
var skill = AgentSkill.FromAssembly(
    typeof(MyApp).Assembly, 
    "MyApp.Skills.PdfProcessing");

// From in-memory files (testing, dynamic generation)
var skill = AgentSkill.FromFiles(new Dictionary<string, string>
{
    ["SKILL.md"] = "---\nname: test\ndescription: Test skill\n---\n# Instructions",
    ["scripts/run.sh"] = "echo Running..."
});

// With name override (uses provided name instead of SKILL.md frontmatter)
var skill = AgentSkill.FromPath("C:/skills/pdf-processing", name: "custom-name");
```

### SkillMetadata

Parsed from SKILL.md frontmatter:

```csharp
public class SkillMetadata
{
    public required string Name { get; init; }        // From frontmatter (required)
    public required string Description { get; init; } // From frontmatter (required)  
    public string? Instructions { get; init; }        // Markdown content after frontmatter
    public string? RawContent { get; init; }          // Full SKILL.md content
}
```

### SkillInfo

Runtime information about a mounted skill:

```csharp
public class SkillInfo
{
    public required string Name { get; init; }        // Skill name
    public required string Description { get; init; } // From SKILL.md
    public required string MountPath { get; init; }   // e.g., /.sandbox/skills/pdf-processing
    public required SkillMetadata Metadata { get; init; }
}
```

### ISkillSource

Interface for loading skill files from different sources:

```csharp
public interface ISkillSource
{
    IEnumerable<SkillFile> GetFiles();
}

public record SkillFile(string RelativePath, string Content);
```

**Built-in implementations:**

| Source | Use Case | Example |
|--------|----------|---------|
| `FileSystemSkillSource` | Development, CLI tools | Load from local folder |
| `EmbeddedSkillSource` | Production, NuGet packages | Load from assembly resources |
| `InMemorySkillSource` | Testing, dynamic generation | Load from dictionary |

## SandboxOptions Integration

```csharp
public class SandboxOptions
{
    // ... existing properties ...
    
    /// <summary>
    /// Agent skills to mount into the sandbox filesystem.
    /// Skills are copied to /.sandbox/skills/{name}/ at initialization.
    /// </summary>
    public IReadOnlyList<AgentSkill> Skills { get; init; } = [];
    
    /// <summary>
    /// Base path where skills are mounted. Default: /.sandbox/skills
    /// </summary>
    public string SkillsMountPath { get; init; } = "/.sandbox/skills";
}
```

## Sandbox Implementation

### Skill Mounting

Skills are loaded from their source and copied into the virtual filesystem during Sandbox initialization:

```csharp
public class Sandbox
{
    private readonly List<SkillInfo> _mountedSkills = [];
    
    public Sandbox(string? id = null, SandboxOptions? options = null)
    {
        // ... existing initialization ...
        MountSkills();
    }
    
    public IReadOnlyList<SkillInfo> GetMountedSkills() => _mountedSkills.AsReadOnly();
    
    private void MountSkills()
    {
        if (_options.Skills.Count == 0) return;
        
        _fileSystem.CreateDirectory(_options.SkillsMountPath);
        
        foreach (var skill in _options.Skills)
        {
            var skillInfo = MountSkill(skill);
            _mountedSkills.Add(skillInfo);
        }
    }
    
    private SkillInfo MountSkill(AgentSkill skill)
    {
        // Load all files from skill source
        var files = skill.Source.GetFiles().ToList();
        
        // Parse SKILL.md frontmatter (required)
        var skillMdContent = files.FirstOrDefault(f => 
            f.RelativePath.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase))?.Content;
        
        if (string.IsNullOrEmpty(skillMdContent))
            throw new InvalidSkillException("Skill must contain a SKILL.md file");
        
        var metadata = SkillMetadata.Parse(skillMdContent);
        
        // Use provided name or name from SKILL.md
        var name = skill.Name ?? metadata.Name;
        var mountPath = $"{_options.SkillsMountPath}/{name}";
        
        _fileSystem.CreateDirectory(mountPath);
        
        // Copy all files into virtual filesystem
        foreach (var file in files)
        {
            var normalizedPath = file.RelativePath.Replace('\\', '/');
            var destPath = $"{mountPath}/{normalizedPath}";
            var parentDir = Path.GetDirectoryName(destPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parentDir))
                _fileSystem.CreateDirectory(parentDir);
            _fileSystem.WriteFile(destPath, file.Content);
        }
        
        return new SkillInfo
        {
            Name = name,
            Description = metadata.Description,
            MountPath = mountPath,
            Metadata = metadata
        };
    }
}
```

## Shell Script Execution

A new `sh` built-in command interprets shell scripts:

### ShCommand Implementation

```csharp
// Built into SandboxShell, not as an extension (shell is not aware of skills)

private ShellResult ExecuteSh(string[] args)
{
    if (args.Length == 0)
    {
        return ShellResult.Error("sh: missing script path");
    }
    
    var scriptPath = ResolvePath(args[0]);
    
    if (!_fileSystem.FileExists(scriptPath))
    {
        return ShellResult.Error($"sh: {args[0]}: No such file");
    }
    
    var scriptContent = _fileSystem.ReadFile(scriptPath, Encoding.UTF8);
    var scriptArgs = args.Skip(1).ToArray();
    
    return ExecuteScript(scriptContent, scriptArgs);
}

private ShellResult ExecuteScript(string script, string[] args)
{
    var lines = script.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    var output = new StringBuilder();
    
    // Set positional parameters $1, $2, etc.
    for (int i = 0; i < args.Length; i++)
    {
        _environment[$"{i + 1}"] = args[i];
    }
    _environment["@"] = string.Join(" ", args);
    _environment["#"] = args.Length.ToString();
    
    foreach (var line in lines)
    {
        var trimmed = line.Trim();
        
        // Skip empty lines and comments
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            continue;
        
        // Skip shebang
        if (trimmed.StartsWith("#!"))
            continue;
        
        var result = Execute(trimmed);
        
        if (!string.IsNullOrEmpty(result.Output))
        {
            output.AppendLine(result.Output);
        }
        
        // Stop on error (set -e behavior)
        if (result.ExitCode != 0)
        {
            return new ShellResult
            {
                ExitCode = result.ExitCode,
                Output = output.ToString().TrimEnd(),
                Error = result.Error
            };
        }
    }
    
    return ShellResult.Ok(output.ToString().TrimEnd());
}
```

### Script Execution via ./path

When a command starts with `./` or `/` and points to a `.sh` file, execute it as a script:

```csharp
// In SandboxShell.Execute()
if ((command.StartsWith("./") || command.StartsWith("/")) && command.EndsWith(".sh"))
{
    return ExecuteSh(new[] { command }.Concat(args).ToArray());
}
```

## Semantic Kernel Integration

### Dynamic Tool Description

The key insight is that **available skills should be embedded in the `GetSkill` tool's description** so agents immediately know what skills exist without needing a separate discovery call.

### CreateGetSkillFunction

```csharp
// In KernelExtensions.cs

/// <summary>
/// Creates a GetSkill function with available skills listed in its description.
/// </summary>
public static AIFunction CreateGetSkillFunction(Sandbox sandbox)
{
    var skills = sandbox.GetMountedSkills();
    var description = BuildSkillFunctionDescription(skills);
    
    return AIFunctionFactory.Create(
        (string skillName) => GetSkillFunction(sandbox, skillName),
        new AIFunctionFactoryCreateOptions
        {
            Name = "get_skill",
            Description = description
        });
}

private static string BuildSkillFunctionDescription(IReadOnlyList<SkillInfo> skills)
{
    if (skills.Count == 0)
        return "Gets detailed information about an agent skill. No skills are currently available.";
    
    var sb = new StringBuilder();
    sb.AppendLine("Gets detailed information about an agent skill.");
    sb.AppendLine("Available skills:");
    
    foreach (var skill in skills)
        sb.AppendLine($"  - {skill.Name}: {skill.Description}");
    
    return sb.ToString();
}

private static string GetSkillFunction(Sandbox sandbox, string skillName)
{
    var skill = sandbox.GetMountedSkills()
        .FirstOrDefault(s => s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));
    
    if (skill == null)
        return JsonSerializer.Serialize(new { error = $"Skill '{skillName}' not found" });
    
    return JsonSerializer.Serialize(new
    {
        name = skill.Name,
        description = skill.Description,
        mountPath = skill.MountPath,
        instructions = skill.Metadata.Instructions
    });
}
```

### Registering the Skill Function

```csharp
// Create sandbox with skills
var options = new SandboxOptions
{
    Skills = new[]
    {
        AgentSkill.FromPath("C:/skills/python-dev"),
        AgentSkill.FromAssembly(typeof(MyApp).Assembly, "MyApp.Skills.GitWorkflow")
    }
};
var sandbox = new Sandbox("agent-1", options);

// Create functions
var executeFunction = KernelExtensions.CreateSandboxFunction(sandbox);
var skillFunction = KernelExtensions.CreateGetSkillFunction(sandbox);

// Register with ChatClient or Kernel
```

### What the Agent Sees

When the agent inspects its available tools, it sees:

```
Tool: get_skill
Description: Gets detailed information about an agent skill.
Available skills:
  - python-dev: Python development environment setup and tooling
  - git-workflow: Git branching and PR workflow

Parameters:
  - skillName (required): The name of the skill to retrieve
```

The agent uses the regular `sandbox_execute` function to run skill scripts:
```
sandbox_execute("sh /.sandbox/skills/python-dev/scripts/setup.sh")
```

This approach:
- Eliminates redundant `ExecuteSkillScript` function
- Agent discovers skills via `get_skill` tool description
- Agent runs scripts via existing sandbox shell

## Usage Examples

### From Local Filesystem (Development)

```csharp
var options = new SandboxOptions
{
    Skills = new[]
    {
        AgentSkill.FromPath("C:/skills/python-dev"),
        AgentSkill.FromPath("C:/skills/git-workflow", name: "custom-name")
    }
};

var sandbox = new Sandbox("agent-1", options);
```

### From Embedded Assembly Resources (Production)

```csharp
// Skills embedded at: MyApp.Skills.PythonDev.SKILL.md, MyApp.Skills.PythonDev.scripts.setup.sh, etc.
var options = new SandboxOptions
{
    Skills = new[]
    {
        AgentSkill.FromAssembly(typeof(MyApp).Assembly, "MyApp.Skills.PythonDev"),
        AgentSkill.FromAssembly(typeof(MyApp).Assembly, "MyApp.Skills.GitWorkflow")
    }
};
```

### From In-Memory (Testing)

```csharp
var options = new SandboxOptions
{
    Skills = new[]
    {
        AgentSkill.FromFiles(new Dictionary<string, string>
        {
            ["SKILL.md"] = """
                ---
                name: test-skill
                description: A test skill for unit tests
                ---
                # Test Skill Instructions
                Run the script at scripts/run.sh
                """,
            ["scripts/run.sh"] = "echo Hello from test skill"
        })
    }
};
```

### Skill Folder Structure

```
python-dev/
├── SKILL.md              # Required: metadata + instructions
├── scripts/
│   ├── setup-venv.sh
│   ├── lint.sh
│   └── test.sh
├── references/
│   └── best-practices.md
└── assets/
    ├── pyproject-template.toml
    └── .gitignore
```

### SKILL.md Example

```markdown
---
name: python-dev
description: Python development environment setup and tooling
---

# Python Development Skill

## When to use
Use this skill when setting up Python development environments.

## Available scripts
- `scripts/setup-venv.sh [python_version]` - Creates virtual environment
- `scripts/lint.sh` - Runs ruff and mypy
- `scripts/test.sh` - Runs pytest

## Setup instructions
1. Run the setup script: `sh /.sandbox/skills/python-dev/scripts/setup-venv.sh`
2. Activate the environment (simulated in sandbox)
3. Install dependencies with pip
```

### Agent Interaction Flow

```
Agent: [Inspects available tools, sees get_skill with description listing available skills]

Tool Description seen by Agent:
  "Gets detailed information about an agent skill including instructions and available scripts.
   Available skills:
     - python-dev: Python development environment setup and tooling
     - git-workflow: Git branching and PR workflow"

Agent: [Decides to use python-dev skill, calls get_skill("python-dev")]

Response: {
  "name": "python-dev",
  "description": "Python development environment setup and tooling",
  "mountPath": "/.sandbox/skills/python-dev",
  "instructions": "# Python Development Skill\n\nUse this skill when setting up Python development environments..."
}

Agent: [Reads instructions, decides to run setup script via sandbox execute]
Agent: [Calls sandbox_execute("sh /.sandbox/skills/python-dev/scripts/setup-venv.sh")]

Response: {
  "exitCode": 0,
  "output": "Created virtual environment at /workspace/.venv",
  "error": null
}
```

## Implementation Checklist

- [x] Create `AgentSandbox.Core/Skills/` directory
  - [x] `AgentSkill.cs` - Factory methods for creating skills from various sources
  - [x] `SkillInfo.cs` - Runtime info model
  - [x] `SkillMetadata.cs` - Parsed SKILL.md frontmatter
  - [x] `ISkillSource.cs` - Interface and SkillFile record
  - [x] `FileSystemSkillSource.cs` - Load from local filesystem
  - [x] `EmbeddedSkillSource.cs` - Load from assembly resources
  - [x] `InMemorySkillSource.cs` - Load from dictionary
  - [x] `InvalidSkillException.cs` - Validation exception
- [x] Update `SandboxOptions.cs`
  - [x] Add `Skills` property
  - [x] Add `SkillsMountPath` property
  - [x] Update `Clone()` method
- [x] Update `Sandbox.cs`
  - [x] Add `_mountedSkills` field
  - [x] Add `GetMountedSkills()` method
  - [x] Implement `MountSkills()` in constructor
  - [x] Implement skill mounting from `ISkillSource`
- [x] Update `SandboxShell.cs`
  - [x] Add `sh` command
  - [x] Add `.sh` file execution support
  - [x] Add script positional parameters ($1, $2, $@, $#, $*)
- [x] Update `AgentSandbox.SemanticKernel/KernelExtensions.cs`
  - [x] Add `CreateGetSkillFunction()` with dynamic description
  - [x] Add `GetSkillFunction()` extension method
- [x] Add unit tests
  - [x] Skill mounting tests (22 tests)
  - [x] sh command tests (16 tests)
- [x] Update documentation
  - [x] Update `DESIGN.md` to reference this doc

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Copy, not lazy mount** | Simpler implementation, skills are typically small, full isolation |
| **Mount to `/.sandbox/skills/`** | Hidden namespace, won't conflict with user files |
| **Shell executes scripts** | Shell is not skill-aware; `sh` is a generic command |
| **SKILL.md frontmatter** | Follows agentskills.io specification |
| **ISkillSource abstraction** | Supports filesystem, embedded resources, and in-memory for testing |
| **Factory methods** | `FromPath`, `FromAssembly`, `FromFiles` provide clean API |
| **Skills in tool description** | Agent sees available skills immediately without extra discovery call |
| **No ExecuteSkillScript function** | Agent uses regular sandbox Execute() with `sh` command - simpler, no redundant API |
| **Dynamic function creation** | Description includes skill list, generated at registration time |
| **Stop on error in scripts** | Default `set -e` behavior for safety |

## Future Considerations

1. **Skill Validation**: Validate skill folder structure before mounting
2. **Skill Versioning**: Track skill versions for reproducibility
3. **Skill Dependencies**: Allow skills to depend on other skills
4. **Skill Caching**: Cache parsed metadata for performance
5. **Script Timeout**: Add timeout for script execution
6. **Conditional Execution**: Support `if`/`then`/`else` in shell scripts
7. **Remote Skills**: Fetch skills from URLs (with security controls)
