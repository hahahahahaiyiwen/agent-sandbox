using System.Text;

namespace AgentSandbox.Core.Shell.Commands;

/// <summary>
/// Show last lines of file command.
/// </summary>
public class TailCommand : IShellCommand
{
    public string Name => "tail";
    public string Description => "Show last lines of file";
    public string Usage => """
        tail [-n N] <file>...

        Options:
          -n N    Show last N lines (default: 10)
        """;

    public ShellResult Execute(string[] args, IShellContext context)
    {
        var lines = 10;
        var paths = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-n" && i + 1 < args.Length)
            {
                int.TryParse(args[++i], out lines);
            }
            else if (!args[i].StartsWith('-'))
            {
                paths.Add(args[i]);
            }
        }

        if (paths.Count == 0)
            return ShellResult.Error("tail: missing file operand");

        var output = new StringBuilder();
        foreach (var p in paths)
        {
            var path = context.ResolvePath(p);
            var content = context.FileSystem.ReadFile(path, Encoding.UTF8);
            var allLines = content.Split('\n');
            var fileLines = allLines.Skip(Math.Max(0, allLines.Length - lines));
            output.AppendLine(string.Join("\n", fileLines));
        }

        return ShellResult.Ok(output.ToString().TrimEnd());
    }
}
