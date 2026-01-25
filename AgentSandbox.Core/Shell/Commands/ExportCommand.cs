namespace AgentSandbox.Core.Shell.Commands;

/// <summary>
/// Set environment variable command.
/// </summary>
public class ExportCommand : IShellCommand
{
    public string Name => "export";
    public string Description => "Set environment variable";
    public string Usage => "export VAR=value";

    public ShellResult Execute(string[] args, IShellContext context)
    {
        foreach (var arg in args)
        {
            var parts = arg.Split('=', 2);
            if (parts.Length == 2)
            {
                context.Environment[parts[0]] = parts[1];
            }
        }
        return ShellResult.Ok();
    }
}
