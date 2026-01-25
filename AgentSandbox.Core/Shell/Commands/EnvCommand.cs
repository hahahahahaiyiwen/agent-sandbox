using System.Text;

namespace AgentSandbox.Core.Shell.Commands;

/// <summary>
/// Show environment variables command.
/// </summary>
public class EnvCommand : IShellCommand
{
    public string Name => "env";
    public string Description => "Show environment variables";
    public string Usage => "env";

    public ShellResult Execute(string[] args, IShellContext context)
    {
        var output = new StringBuilder();
        foreach (var kvp in context.Environment.OrderBy(k => k.Key))
        {
            output.AppendLine($"{kvp.Key}={kvp.Value}");
        }
        return ShellResult.Ok(output.ToString().TrimEnd());
    }
}
