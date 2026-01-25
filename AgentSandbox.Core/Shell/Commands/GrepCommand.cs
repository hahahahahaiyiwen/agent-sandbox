using System.Text;
using System.Text.RegularExpressions;

namespace AgentSandbox.Core.Shell.Commands;

/// <summary>
/// Search for pattern in files command.
/// </summary>
public class GrepCommand : IShellCommand
{
    public string Name => "grep";
    public string Description => "Search for pattern in files";
    public string Usage => """
        grep [-inr] <pattern> <file|dir>...

        Options:
          -i    Case insensitive search
          -n    Show line numbers
          -r    Search directories recursively
        """;

    public ShellResult Execute(string[] args, IShellContext context)
    {
        // Parse flags first, then get pattern and files
        var ignoreCase = false;
        var showLineNumbers = false;
        var recursive = false;
        var nonFlagArgs = new List<string>();

        foreach (var arg in args)
        {
            if (arg == "-i")
                ignoreCase = true;
            else if (arg == "-n")
                showLineNumbers = true;
            else if (arg == "-r" || arg == "-R")
                recursive = true;
            else if (arg.StartsWith("-"))
            {
                // Check for combined flags like -rn, -in, -rin
                foreach (var c in arg.Skip(1))
                {
                    if (c == 'i') ignoreCase = true;
                    else if (c == 'n') showLineNumbers = true;
                    else if (c == 'r' || c == 'R') recursive = true;
                }
            }
            else
                nonFlagArgs.Add(arg);
        }

        if (nonFlagArgs.Count < 2)
            return ShellResult.Error("grep: missing pattern or file");

        var pattern = nonFlagArgs[0];
        var inputPaths = nonFlagArgs.Skip(1).ToList();

        // Expand directories if recursive
        var filePaths = new List<(string DisplayPath, string FullPath)>();
        foreach (var p in inputPaths)
        {
            var path = context.ResolvePath(p);
            if (context.FileSystem.IsDirectory(path))
            {
                if (recursive)
                {
                    CollectFilesRecursive(context, path, p, filePaths);
                }
                else
                {
                    return ShellResult.Error($"grep: {p}: Is a directory");
                }
            }
            else
            {
                filePaths.Add((p, path));
            }
        }

        var output = new StringBuilder();
        var regexOptions = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        var regex = new Regex(pattern, regexOptions);
        var showPrefix = filePaths.Count > 1;

        foreach (var (displayPath, fullPath) in filePaths)
        {
            try
            {
                var content = context.FileSystem.ReadFile(fullPath, Encoding.UTF8);
                var lines = content.Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    if (regex.IsMatch(lines[i]))
                    {
                        var prefix = showPrefix ? $"{displayPath}:" : "";
                        var lineNum = showLineNumbers ? $"{i + 1}:" : "";
                        output.AppendLine($"{prefix}{lineNum}{lines[i]}");
                    }
                }
            }
            catch (FileNotFoundException)
            {
                return ShellResult.Error($"grep: {displayPath}: No such file or directory");
            }
        }

        return ShellResult.Ok(output.ToString().TrimEnd());
    }

    private static void CollectFilesRecursive(IShellContext context, string path, string displayPath, List<(string DisplayPath, string FullPath)> files)
    {
        if (!context.FileSystem.IsDirectory(path))
        {
            files.Add((displayPath, path));
            return;
        }

        foreach (var entry in context.FileSystem.ListDirectory(path))
        {
            var fullPath = path == "/" ? "/" + entry : path + "/" + entry;
            var childDisplayPath = displayPath == "." ? entry : $"{displayPath}/{entry}";

            if (context.FileSystem.IsDirectory(fullPath))
            {
                CollectFilesRecursive(context, fullPath, childDisplayPath, files);
            }
            else
            {
                files.Add((childDisplayPath, fullPath));
            }
        }
    }
}
