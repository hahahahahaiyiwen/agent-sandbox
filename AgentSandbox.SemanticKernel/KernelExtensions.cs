using AgentSandbox.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using System.Text;

namespace Microsoft.SemanticKernel.Extensions.AgentSandbox;

/// <summary>
/// Extension methods for registering Sandbox with Semantic Kernel.
/// </summary>
public static class KernelExtensions
{
    /// <summary>
    /// Adds SandboxManager as a singleton service to the kernel.
    /// Each sandbox is created on-demand via the manager.
    /// </summary>
    /// <param name="builder">The kernel builder.</param>
    /// <param name="defaultOptions">Default options for new sandboxes.</param>
    /// <returns>The kernel builder for chaining.</returns>
    public static IKernelBuilder AddSandboxManager(
        this IKernelBuilder builder,
        SandboxOptions? defaultOptions = null)
    {
        builder.Services.AddSingleton(_ => new SandboxManager(defaultOptions));

        builder.Services.AddScoped<Sandbox>(sp =>
        {
            var manager = sp.GetRequiredService<SandboxManager>();
            return manager.Create();
        });

        return builder;
    }

    /// <summary>
    /// Creates a KernelFunction for sandbox command execution.
    /// </summary>
    /// <param name="kernel">The kernel instance.</param>
    /// <param name="options">Optional sandbox options for description generation.</param>
    /// <returns>A KernelFunction that executes commands in the sandbox.</returns>
    public static KernelFunction GetSandboxFunction(this Kernel kernel, SandboxOptions? options = null)
    {
        var sandbox = kernel.Services.GetRequiredService<Sandbox>();

        if (sandbox == null)
        {
            throw new InvalidOperationException("Sandbox service is not registered. Please call AddSandboxManager() when configuring the kernel.");
        }

        return CreateSandboxFunction(sandbox).AsKernelFunction();
    }

    /// <summary>
    /// Creates a AIFunction for sandbox command execution with dynamic description.
    /// </summary>
    /// <param name="sandbox">The sandbox instance.</param>
    /// <returns>An AIFunction that executes commands in the sandbox.</returns>
    public static AIFunction CreateSandboxFunction(Sandbox sandbox)
    {
        return AIFunctionFactory.Create(
            (string command) =>
            {
                var result = sandbox.Execute(command);
                if (result.Success)
                {
                    return string.IsNullOrEmpty(result.Stdout)
                        ? "(command completed successfully)"
                        : result.Stdout;
                }
                return $"Error: {result.Stderr}";
            },
            name: "Execute",
            description: BuildSandboxFunctionDescription(sandbox.Options));
    }

    private static string BuildSandboxFunctionDescription(SandboxOptions? options)
    {
        var builder = new StringBuilder();

        builder.Append("Execute a command in a bash terminal. ");
        builder.Append("Available commands: ls, cat, mkdir, rm, cp, mv, echo, grep, find, head, tail, wc, pwd, cd, touch, env, export. ");
        builder.Append("Use > to write output to file, >> to append.");

        if (options?.ShellExtensions?.Any() == true)
        {
            builder.AppendLine();
            builder.AppendLine("Extensions:");
            foreach (var extension in options.ShellExtensions)
            {
                builder.AppendLine($"- {extension.Name}: {extension.Description}");
            }
        }

        return builder.ToString();
    }
}
