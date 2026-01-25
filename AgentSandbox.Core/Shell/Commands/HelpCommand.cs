using System.Text;

namespace AgentSandbox.Core.Shell.Commands;

/// <summary>
/// Show available commands help.
/// </summary>
public class HelpCommand : IShellCommand
{
    public string Name => "help";
    public string Description => "Show available commands";
    public string Usage => "help";

    public ShellResult Execute(string[] args, IShellContext context)
    {
        var output = new StringBuilder();
        output.AppendLine("Available commands:");
        output.AppendLine("  pwd              Print working directory");
        output.AppendLine("  cd [dir]         Change directory");
        output.AppendLine("  ls [-la] [path]  List directory contents");
        output.AppendLine("  cat <file>       Display file contents");
        output.AppendLine("  echo [text]      Print text");
        output.AppendLine("  mkdir [-p] <dir> Create directory");
        output.AppendLine("  rm [-rf] <path>  Remove file or directory");
        output.AppendLine("  cp <src> <dest>  Copy file or directory");
        output.AppendLine("  mv <src> <dest>  Move/rename file or directory");
        output.AppendLine("  touch <file>     Create empty file or update timestamp");
        output.AppendLine("  head [-n N] <f>  Show first N lines");
        output.AppendLine("  tail [-n N] <f>  Show last N lines");
        output.AppendLine("  wc <file>        Count lines, words, bytes");
        output.AppendLine("  grep <pat> <f>   Search for pattern in files");
        output.AppendLine("  find [path]      Find files");
        output.AppendLine("  env              Show environment variables");
        output.AppendLine("  export VAR=val   Set environment variable");
        output.AppendLine("  sh <script>      Execute shell script");
        output.AppendLine("  help             Show this help");
        output.AppendLine();
        output.AppendLine("Use '<command> -h' for detailed help on a specific command.");

        // Add extension commands info if available
        if (context is IExtendedShellContext extContext)
        {
            var extensions = extContext.GetExtensionCommands();
            if (extensions.Any())
            {
                output.AppendLine();
                output.AppendLine("Extension commands:");
                foreach (var cmd in extensions.OrderBy(c => c.Name))
                {
                    output.AppendLine($"  {cmd.Name,-16} {cmd.Description}");
                }
            }
        }

        return ShellResult.Ok(output.ToString().TrimEnd());
    }
}

/// <summary>
/// Extended shell context that provides access to extension commands.
/// </summary>
public interface IExtendedShellContext : IShellContext
{
    IEnumerable<IShellCommand> GetExtensionCommands();
}
