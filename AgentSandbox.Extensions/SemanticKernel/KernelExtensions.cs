using AgentSandbox.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace AgentSandbox.Extensions.SemanticKernel;

/// <summary>
/// Extension methods for registering Sandbox with Semantic Kernel.
/// </summary>
public static class KernelExtensions
{
    /// <summary>
    /// Creates a KernelFunction for sandbox command execution.
    /// </summary>
    /// <param name="kernel">The kernel instance.</param>
    /// <returns>A KernelFunction that executes commands in the sandbox.</returns>
    public static KernelFunction GetSandboxFunction(this Kernel kernel)
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
            name: "bash_shell",
            description: sandbox.GetToolDescription());
    }

    /// <summary>
    /// Creates an AIFunction for retrieving skill information.
    /// The function description dynamically includes all available skills.
    /// </summary>
    /// <param name="sandbox">The sandbox instance with loaded skills.</param>
    /// <returns>An AIFunction that retrieves skill information.</returns>
    public static AIFunction CreateGetSkillFunction(Sandbox sandbox)
    {
        return AIFunctionFactory.Create(
            (string skillName) => GetSkillImplementation(sandbox, skillName),
            name: "get_skill",
            description: sandbox.GetSkillsDescription());
    }

    private static string GetSkillImplementation(Sandbox sandbox, string skillName)
    {
        var skill = sandbox.GetSkills()
            .FirstOrDefault(s => s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));

        if (skill?.Metadata?.Instructions == null)
        {
            return $"Skill '{skillName}' not found";
        }

        // Read SKILL.md for instructions (already parsed in metadata)
        return skill.Metadata.Instructions;
    }
}
