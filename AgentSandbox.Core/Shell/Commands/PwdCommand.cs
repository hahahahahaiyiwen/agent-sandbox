namespace AgentSandbox.Core.Shell.Commands;

/// <summary>
/// Print working directory command.
/// </summary>
public class PwdCommand : IShellCommand
{
    public string Name => "pwd";
    public string Description => "Print working directory";
    public string Usage => "pwd";

    public ShellResult Execute(string[] args, IShellContext context)
    {
        return ShellResult.Ok(context.CurrentDirectory);
    }
}
