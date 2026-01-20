# AgentSandbox.Extensions

Extensions for AgentSandbox including Semantic Kernel integration, dependency injection, and observability.

## Installation

```bash
dotnet add package AgentSandbox.Extensions
```

## Semantic Kernel Integration

Register sandbox as AI functions for use with Semantic Kernel or Microsoft.Extensions.AI:

```csharp
using AgentSandbox.Core;
using AgentSandbox.Extensions.SemanticKernel;

var sandbox = new Sandbox();

// Create AI functions
var executeFunction = KernelExtensions.CreateSandboxFunction(sandbox);
var getSkillFunction = KernelExtensions.CreateGetSkillFunction(sandbox);

// Use with ChatClient
var options = new ChatOptions
{
    Tools = [executeFunction, getSkillFunction]
};
```

### With Semantic Kernel

```csharp
var kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion("gpt-4", apiKey)
    .Build();

// Get function from registered sandbox
var function = kernel.GetSandboxFunction();
kernel.Plugins.AddFromFunctions("Sandbox", [function]);
```

## Dependency Injection

```csharp
using AgentSandbox.Extensions.DependencyInjection;

services.AddSandbox(options =>
{
    options.WorkingDirectory = "/workspace";
    options.MaxTotalSize = 1024 * 1024;
});

// Or with sandbox manager for multi-tenant scenarios
services.AddSandboxManager();
```

## Observability

### OpenTelemetry

```csharp
using AgentSandbox.Extensions.Observability;

services.AddOpenTelemetry()
    .WithTracing(builder => builder.AddSandboxInstrumentation())
    .WithMetrics(builder => builder.AddSandboxInstrumentation());
```

### Application Insights

```csharp
services.AddApplicationInsightsTelemetry();
services.AddSandboxApplicationInsights();
```

### Logging

```csharp
services.AddSandboxLogging();
```

## Shell Extensions

Shell command extensions are in `AgentSandbox.Core.ShellExtensions`:

```csharp
using AgentSandbox.Core;
using AgentSandbox.Core.ShellExtensions;

var options = new SandboxOptions
{
    ShellExtensions = [
        new CurlCommand(),  // HTTP requests
        new JqCommand(),    // JSON processing
        new GitCommand()    // Git operations
    ]
};

var sandbox = new Sandbox(options: options);

// Now available in shell
sandbox.Execute("curl https://api.example.com/data");
sandbox.Execute("echo '{\"name\":\"test\"}' | jq '.name'");
sandbox.Execute("git init");
```

## See Also

- [AgentSandbox.Core](../AgentSandbox.Core) - Core sandbox functionality
- [Semantic Kernel Documentation](https://learn.microsoft.com/semantic-kernel/)
