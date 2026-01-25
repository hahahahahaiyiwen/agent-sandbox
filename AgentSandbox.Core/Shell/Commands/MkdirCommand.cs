namespace AgentSandbox.Core.Shell.Commands;

/// <summary>
/// Create directory command.
/// </summary>
public class MkdirCommand : IShellCommand
{
    public string Name => "mkdir";
    public string Description => "Create directory";
    public string Usage => """
        mkdir [-p] <dir>...

        Options:
          -p    Create parent directories as needed
        """;

    public ShellResult Execute(string[] args, IShellContext context)
    {
        var createParents = args.Contains("-p");
        var dirs = args.Where(a => !a.StartsWith("-")).ToArray();

        if (dirs.Length == 0)
            return ShellResult.Error("mkdir: missing operand");

        foreach (var dir in dirs)
        {
            var path = context.ResolvePath(dir);
            
            if (createParents)
            {
                context.FileSystem.CreateDirectory(path);
            }
            else
            {
                var parent = path.Contains('/') ? path[..path.LastIndexOf('/')] : "/";
                if (parent == "") parent = "/";
                
                if (!context.FileSystem.Exists(parent))
                    return ShellResult.Error($"mkdir: cannot create directory '{dir}': No such file or directory");
                
                if (context.FileSystem.Exists(path))
                    return ShellResult.Error($"mkdir: cannot create directory '{dir}': File exists");
                
                context.FileSystem.CreateDirectory(path);
            }
        }

        return ShellResult.Ok();
    }
}
