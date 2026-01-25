namespace AgentSandbox.Core.Shell.Commands;

/// <summary>
/// Create empty file or update timestamp command.
/// </summary>
public class TouchCommand : IShellCommand
{
    public string Name => "touch";
    public string Description => "Create empty file or update timestamp";
    public string Usage => "touch <file>...";

    public ShellResult Execute(string[] args, IShellContext context)
    {
        if (args.Length == 0)
            return ShellResult.Error("touch: missing file operand");

        foreach (var arg in args.Where(a => !a.StartsWith('-')))
        {
            var path = context.ResolvePath(arg);
            
            if (!context.FileSystem.Exists(path))
            {
                context.FileSystem.WriteFile(path, Array.Empty<byte>());
            }
            else
            {
                var entry = context.FileSystem.GetEntry(path);
                if (entry != null) entry.ModifiedAt = DateTime.UtcNow;
            }
        }

        return ShellResult.Ok();
    }
}
