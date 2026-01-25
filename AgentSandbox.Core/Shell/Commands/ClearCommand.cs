namespace AgentSandbox.Core.Shell.Commands;

/// <summary>
/// Clear screen command.
/// </summary>
public class ClearCommand : IShellCommand
{
    public string Name => "clear";
    public string Description => "Clear screen";
    public string Usage => "clear";

    public ShellResult Execute(string[] args, IShellContext context)
    {
        return ShellResult.Ok();
    }
}
