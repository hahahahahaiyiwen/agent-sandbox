namespace AgentSandbox.Core.Shell.Commands;

/// <summary>
/// Print text to output command.
/// </summary>
public class EchoCommand : IShellCommand
{
    public string Name => "echo";
    public string Description => "Print text to output";
    public string Usage => "echo [text...]";

    public ShellResult Execute(string[] args, IShellContext context)
    {
        return ShellResult.Ok(string.Join(" ", args));
    }
}
