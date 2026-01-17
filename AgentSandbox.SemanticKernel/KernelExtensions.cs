using AgentSandbox.Core;
using AgentSandbox.Core.Skills;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using System.Text;
using System.Text.Json;

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

    /// <summary>
    /// Creates an AIFunction for retrieving skill information.
    /// The function description dynamically includes all available skills.
    /// </summary>
    /// <param name="sandbox">The sandbox instance with mounted skills.</param>
    /// <returns>An AIFunction that retrieves skill information.</returns>
    public static AIFunction CreateGetSkillFunction(Sandbox sandbox)
    {
        var skills = sandbox.GetMountedSkills();
        var description = BuildSkillFunctionDescription(skills);

        return AIFunctionFactory.Create(
            (string skillName) => GetSkillImplementation(sandbox, skillName),
            name: "GetSkill",
            description: description);
    }

    /// <summary>
    /// Creates a KernelFunction for retrieving skill information.
    /// The function description dynamically includes all available skills.
    /// </summary>
    /// <param name="kernel">The kernel instance.</param>
    /// <returns>A KernelFunction that retrieves skill information.</returns>
    public static KernelFunction GetSkillFunction(this Kernel kernel)
    {
        var sandbox = kernel.Services.GetRequiredService<Sandbox>();

        if (sandbox == null)
        {
            throw new InvalidOperationException("Sandbox service is not registered. Please call AddSandboxManager() when configuring the kernel.");
        }

        return CreateGetSkillFunction(sandbox).AsKernelFunction();
    }

    private static string BuildSandboxFunctionDescription(SandboxOptions? options)
    {
        var builder = new StringBuilder();

        builder.Append("Execute a command in a bash terminal. ");
        builder.Append("Available commands: ls, cat, mkdir, rm, cp, mv, echo, grep, find, head, tail, wc, pwd, cd, touch, env, export, sh. ");
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

    private static string BuildSkillFunctionDescription(IReadOnlyList<SkillInfo> skills)
    {
        if (skills.Count == 0)
        {
            return "Gets detailed information about an agent skill. No skills are currently available.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Gets detailed information about an agent skill including instructions and available scripts.");
        sb.AppendLine();
        sb.AppendLine("Available skills:");

        foreach (var skill in skills)
        {
            sb.AppendLine($"  - {skill.Name}: {skill.Description}");
        }

        return sb.ToString();
    }

    private static string GetSkillImplementation(Sandbox sandbox, string skillName)
    {
        var skill = sandbox.GetMountedSkills()
            .FirstOrDefault(s => s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));

        if (skill == null)
        {
            return JsonSerializer.Serialize(new { error = $"Skill '{skillName}' not found" });
        }

        // Read SKILL.md for instructions (already parsed in metadata)
        return JsonSerializer.Serialize(new
        {
            name = skill.Name,
            description = skill.Description,
            mountPath = skill.MountPath,
            instructions = skill.Metadata.Instructions
        });
    }
}
