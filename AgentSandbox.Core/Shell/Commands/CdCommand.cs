namespace AgentSandbox.Core.Shell.Commands;

/// <summary>
/// Change directory command.
/// </summary>
public class CdCommand : IShellCommand
{
    public string Name => "cd";
    public string Description => "Change directory";
    public string Usage => """
        cd [dir]
          cd ~     Go to home directory
          cd -     Go to previous directory
        """;

    public ShellResult Execute(string[] args, IShellContext context)
    {
        var target = args.Length > 0 ? args[0] : (context.Environment.TryGetValue("HOME", out var home) ? home : "/");
        
        if (target == "-")
        {
            target = context.Environment.TryGetValue("OLDPWD", out var oldpwd) ? oldpwd : context.CurrentDirectory;
        }

        var path = context.ResolvePath(target);
        
        if (!context.FileSystem.Exists(path))
            return ShellResult.Error($"cd: {target}: No such file or directory");
        
        if (!context.FileSystem.IsDirectory(path))
            return ShellResult.Error($"cd: {target}: Not a directory");

        context.Environment["OLDPWD"] = context.CurrentDirectory;
        context.CurrentDirectory = path;
        context.Environment["PWD"] = context.CurrentDirectory;
        
        return ShellResult.Ok();
    }
}
