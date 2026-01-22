using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AgentSandbox.Core.FileSystem;

namespace AgentSandbox.Core.Shell;

/// <summary>
/// A sandboxed shell that executes commands against a virtual filesystem.
/// Emulates common Unix commands without touching the real filesystem.
/// Supports extensibility via IShellCommand registration.
/// </summary>
public class SandboxShell : ISandboxShell, IShellContext
{
    private readonly IFileSystem _fs;
    private string _currentDirectory = "/";
    private readonly Dictionary<string, string> _environment = new();
    private readonly Dictionary<string, (Func<string[], ShellResult> Handler, string Help)> _builtinCommands;
    private readonly Dictionary<string, IShellCommand> _extensionCommands = new();

    public string CurrentDirectory
    {
        get => _currentDirectory;
        set => _currentDirectory = FileSystemPath.Normalize(value);
    }
    
    public IReadOnlyDictionary<string, string> Environment => _environment;
    
    // IShellContext implementation
    IFileSystem IShellContext.FileSystem => _fs;
    IDictionary<string, string> IShellContext.Environment => _environment;

    public SandboxShell(IFileSystem fileSystem)
    {
        _fs = fileSystem;
        
        _environment["HOME"] = "/home";
        _environment["PATH"] = "/bin:/usr/bin";
        _environment["PWD"] = _currentDirectory;

        _builtinCommands = new Dictionary<string, (Func<string[], ShellResult> Handler, string Help)>
        {
            ["pwd"] = (CmdPwd, """
                pwd - Print working directory

                Usage: pwd
                """),
            ["cd"] = (CmdCd, """
                cd - Change directory

                Usage: cd [dir]
                  cd ~     Go to home directory
                  cd -     Go to previous directory
                """),
            ["ls"] = (CmdLs, """
                ls - List directory contents

                Usage: ls [-laR] [path...]

                Options:
                  -a    Show hidden files (starting with .)
                  -l    Long format with details
                  -R    List subdirectories recursively
                """),
            ["cat"] = (CmdCat, """
                cat - Display file contents

                Usage: cat <file>...
                """),
            ["echo"] = (CmdEcho, """
                echo - Print text to output

                Usage: echo [text...]
                """),
            ["mkdir"] = (CmdMkdir, """
                mkdir - Create directory

                Usage: mkdir [-p] <dir>...

                Options:
                  -p    Create parent directories as needed
                """),
            ["rm"] = (CmdRm, """
                rm - Remove files or directories

                Usage: rm [-rf] <path>...

                Options:
                  -r, -R    Remove directories recursively
                  -f        Force, ignore nonexistent files
                """),
            ["cp"] = (CmdCp, """
                cp - Copy files or directories

                Usage: cp [-r] <source>... <dest>

                Options:
                  -r, -R    Copy directories recursively
                """),
            ["mv"] = (CmdMv, """
                mv - Move/rename files or directories

                Usage: mv <source>... <dest>
                """),
            ["touch"] = (CmdTouch, """
                touch - Create empty file or update timestamp

                Usage: touch <file>...
                """),
            ["head"] = (CmdHead, """
                head - Show first lines of file

                Usage: head [-n N] <file>...

                Options:
                  -n N    Show first N lines (default: 10)
                """),
            ["tail"] = (CmdTail, """
                tail - Show last lines of file

                Usage: tail [-n N] <file>...

                Options:
                  -n N    Show last N lines (default: 10)
                """),
            ["wc"] = (CmdWc, """
                wc - Count lines, words, and bytes

                Usage: wc <file>...
                """),
            ["grep"] = (CmdGrep, """
                grep - Search for pattern in files

                Usage: grep [-inr] <pattern> <file|dir>...

                Options:
                  -i    Case insensitive search
                  -n    Show line numbers
                  -r    Search directories recursively
                """),
            ["find"] = (CmdFind, """
                find - Find files by name

                Usage: find [path] [-name pattern]

                Options:
                  -name <pattern>    Filter by filename pattern (supports * and ?)
                """),
            ["env"] = (CmdEnv, """
                env - Show environment variables

                Usage: env
                """),
            ["export"] = (CmdExport, """
                export - Set environment variable

                Usage: export VAR=value
                """),
            ["clear"] = (_ => ShellResult.Ok(), """
                clear - Clear screen

                Usage: clear
                """),
            ["help"] = (CmdHelp, """
                help - Show available commands

                Usage: help

                Tip: Use '<command> -h' for help on a specific command.
                """),
            ["sh"] = (CmdSh, """
                sh - Execute shell script

                Usage: sh <script.sh> [args...]
                """),
        };
    }

    #region Command Registration

    /// <summary>
    /// Registers a shell command extension.
    /// </summary>
    public void RegisterCommand(IShellCommand command)
    {
        _extensionCommands[command.Name.ToLowerInvariant()] = command;
        foreach (var alias in command.Aliases)
        {
            _extensionCommands[alias.ToLowerInvariant()] = command;
        }
    }

    /// <summary>
    /// Registers multiple shell command extensions.
    /// </summary>
    public void RegisterCommands(IEnumerable<IShellCommand> commands)
    {
        foreach (var command in commands)
        {
            RegisterCommand(command);
        }
    }

    /// <summary>
    /// Gets all available command names (built-in and extensions).
    /// </summary>
    public IEnumerable<string> GetAvailableCommands()
    {
        return _builtinCommands.Keys
            .Concat(_extensionCommands.Values.Select(c => c.Name).Distinct())
            .OrderBy(c => c);
    }

    #endregion

    /// <summary>
    /// Resolves a path relative to the current directory.
    /// </summary>
    public string ResolvePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return _currentDirectory;
        if (path.StartsWith('/')) return FileSystemPath.Normalize(path);
        
        var combined = _currentDirectory == "/" 
            ? "/" + path 
            : _currentDirectory + "/" + path;
        
        return FileSystemPath.Normalize(combined);
    }

    /// <summary>
    /// Executes a command string.
    /// </summary>
    public ShellResult Execute(string commandLine)
    {
        var sw = Stopwatch.StartNew();
        
        if (string.IsNullOrWhiteSpace(commandLine))
            return ShellResult.Ok();

        // Check for pipeline operator (not supported)
        var pipeIndex = FindUnquotedOperator(commandLine, "|");
        if (pipeIndex >= 0 && (pipeIndex + 1 >= commandLine.Length || commandLine[pipeIndex + 1] != '|'))
        {
            // Found | but not || (which would be logical OR, also unsupported but different error)
            return ShellResult.Error(
                "Pipelines are not supported. Workarounds:\n" +
                "  - Use file arguments: 'grep pattern file.txt' instead of 'cat file.txt | grep pattern'\n" +
                "  - Execute commands separately and process output programmatically\n" +
                "  - Use shell scripts (.sh) to sequence commands");
        }

        // Check for output redirection (ignore > inside quotes)
        string? redirectFile = null;
        bool appendMode = false;
        var redirectIndex = FindUnquotedOperator(commandLine, ">>");
        if (redirectIndex > 0)
        {
            appendMode = true;
            redirectFile = commandLine[(redirectIndex + 2)..].Trim().Trim('"', '\'');
            commandLine = commandLine[..redirectIndex].Trim();
        }
        else
        {
            redirectIndex = FindUnquotedOperator(commandLine, ">");
            if (redirectIndex > 0)
            {
                redirectFile = commandLine[(redirectIndex + 1)..].Trim().Trim('"', '\'');
                commandLine = commandLine[..redirectIndex].Trim();
            }
        }

        // Simple command parsing (doesn't handle all edge cases)
        var (parts, wasQuoted) = ParseCommandLineWithQuoteInfo(commandLine);
        if (parts.Length == 0)
            return ShellResult.Ok();

        var cmd = parts[0];
        var cmdLower = cmd.ToLowerInvariant();
        
        // Expand globs in arguments (skip command name, skip quoted args)
        var args = ExpandGlobs(parts.Skip(1).ToArray(), wasQuoted.Skip(1).ToArray());

        ShellResult result;
        
        // Check if it's a direct script execution (./script.sh or /path/to/script.sh)
        if ((cmd.StartsWith("./") || cmd.StartsWith("/")) && cmd.EndsWith(".sh"))
        {
            try
            {
                // Execute as shell script
                result = CmdSh(new[] { cmd }.Concat(args).ToArray());
            }
            catch (Exception ex)
            {
                result = ShellResult.Error($"{cmd}: {ex.Message}");
            }
        }
        // Check built-in commands first
        else if (_builtinCommands.TryGetValue(cmdLower, out var command))
        {
            try
            {
                // Check for -h help argument
                if (args.Length > 0 && args[0] == "-h")
                {
                    result = ShellResult.Ok(command.Help);
                }
                else
                {
                    result = command.Handler(args);
                }
            }
            catch (Exception ex)
            {
                result = ShellResult.Error($"{cmdLower}: {ex.Message}");
            }
        }
        // Then check extension commands
        else if (_extensionCommands.TryGetValue(cmdLower, out var extCommand))
        {
            try
            {
                result = extCommand.Execute(args, this);
            }
            catch (Exception ex)
            {
                result = ShellResult.Error($"{cmdLower}: {ex.Message}");
            }
        }
        else
        {
            result = ShellResult.Error($"{cmdLower}: command not found", 127);
        }

        // Handle output redirection
        if (redirectFile != null && result.Success && !string.IsNullOrEmpty(result.Stdout))
        {
            try
            {
                var path = ResolvePath(redirectFile);
                if (appendMode)
                {
                    _fs.AppendToFile(path, result.Stdout);
                }
                else
                {
                    _fs.WriteFile(path, result.Stdout);
                }
                result = ShellResult.Ok(); // Clear stdout since it was redirected
            }
            catch (Exception ex)
            {
                result = ShellResult.Error($"redirect: {ex.Message}");
            }
        }

        result.Command = commandLine;
        result.Duration = sw.Elapsed;
        return result;
    }

    private string[] ParseCommandLine(string commandLine)
    {
        var (parts, _) = ParseCommandLineWithQuoteInfo(commandLine);
        return parts;
    }

    private (string[] Parts, bool[] WasQuoted) ParseCommandLineWithQuoteInfo(string commandLine)
    {
        var parts = new List<string>();
        var wasQuoted = new List<bool>();
        var current = new StringBuilder();
        var inQuote = false;
        var quoteChar = '\0';
        var currentWasQuoted = false;

        for (int i = 0; i < commandLine.Length; i++)
        {
            var c = commandLine[i];

            if (inQuote)
            {
                // Handle escape sequences inside quotes
                if (c == '\\' && i + 1 < commandLine.Length)
                {
                    var next = commandLine[i + 1];
                    var escaped = GetEscapedChar(next);
                    if (escaped.HasValue)
                    {
                        current.Append(escaped.Value);
                        i++; // Skip the next character
                        continue;
                    }
                    // Not a recognized escape - treat backslash literally
                }

                if (c == quoteChar)
                {
                    inQuote = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"' || c == '\'')
            {
                inQuote = true;
                quoteChar = c;
                currentWasQuoted = true;
            }
            else if (c == '\\' && i + 1 < commandLine.Length)
            {
                // Handle escape sequences outside quotes
                var next = commandLine[i + 1];
                var escaped = GetEscapedChar(next);
                if (escaped.HasValue)
                {
                    current.Append(escaped.Value);
                    i++; // Skip the next character
                    continue;
                }
                // Not a recognized escape - treat backslash literally
                current.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    parts.Add(ExpandVariables(current.ToString()));
                    wasQuoted.Add(currentWasQuoted);
                    current.Clear();
                    currentWasQuoted = false;
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            parts.Add(ExpandVariables(current.ToString()));
            wasQuoted.Add(currentWasQuoted);
        }

        return (parts.ToArray(), wasQuoted.ToArray());
    }

    /// <summary>
    /// Returns the character for a recognized escape sequence, or null if not recognized.
    /// </summary>
    private static char? GetEscapedChar(char c)
    {
        return c switch
        {
            'n' => '\n',
            't' => '\t',
            'r' => '\r',
            '\\' => '\\',
            '"' => '"',
            '\'' => '\'',
            ' ' => ' ',
            _ => null
        };
    }

    /// <summary>
    /// Expands glob patterns in arguments. Quoted arguments are not expanded.
    /// </summary>
    private string[] ExpandGlobs(string[] args, bool[] wasQuoted)
    {
        var result = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var quoted = i < wasQuoted.Length && wasQuoted[i];

            // Don't expand if quoted or if it's a flag
            if (quoted || arg.StartsWith("-"))
            {
                result.Add(arg);
                continue;
            }

            // Check if contains glob characters
            if (!ContainsGlobChars(arg))
            {
                result.Add(arg);
                continue;
            }

            // Expand the glob
            var matches = ExpandGlobPattern(arg);
            if (matches.Count > 0)
            {
                result.AddRange(matches);
            }
            else
            {
                // No matches - keep original (like bash behavior)
                result.Add(arg);
            }
        }

        return result.ToArray();
    }

    private static bool ContainsGlobChars(string s)
    {
        return s.Contains('*') || s.Contains('?') || s.Contains('[');
    }

    /// <summary>
    /// Expands a glob pattern against the virtual filesystem.
    /// </summary>
    private List<string> ExpandGlobPattern(string pattern)
    {
        var results = new List<string>();

        // Determine base path and pattern
        string basePath;
        string globPattern;

        if (pattern.StartsWith("/"))
        {
            // Absolute path - find the non-glob prefix
            var lastSlashBeforeGlob = FindLastSlashBeforeGlob(pattern);
            if (lastSlashBeforeGlob == 0)
            {
                basePath = "/";
                globPattern = pattern[1..];
            }
            else
            {
                basePath = pattern[..lastSlashBeforeGlob];
                globPattern = pattern[(lastSlashBeforeGlob + 1)..];
            }
        }
        else
        {
            // Relative path
            var slashIndex = FindLastSlashBeforeGlob(pattern);
            if (slashIndex < 0)
            {
                basePath = _currentDirectory;
                globPattern = pattern;
            }
            else
            {
                var relativePart = pattern[..slashIndex];
                basePath = ResolvePath(relativePart);
                globPattern = pattern[(slashIndex + 1)..];
            }
        }

        // If pattern contains path separators, we need recursive matching
        if (globPattern.Contains('/'))
        {
            ExpandGlobRecursive(basePath, globPattern.Split('/'), 0, results, pattern.StartsWith("/"));
        }
        else
        {
            // Simple case - single level glob
            ExpandGlobSingleLevel(basePath, globPattern, results, pattern.StartsWith("/"));
        }

        results.Sort();
        return results;
    }

    private static int FindLastSlashBeforeGlob(string pattern)
    {
        int lastSlash = -1;
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '/')
                lastSlash = i;
            if (pattern[i] == '*' || pattern[i] == '?' || pattern[i] == '[')
                break;
        }
        return lastSlash;
    }

    private void ExpandGlobSingleLevel(string basePath, string pattern, List<string> results, bool absolute)
    {
        if (!_fs.IsDirectory(basePath))
            return;

        var regex = GlobToRegex(pattern);

        foreach (var name in _fs.ListDirectory(basePath))
        {
            if (regex.IsMatch(name))
            {
                var fullPath = basePath == "/" ? "/" + name : basePath + "/" + name;
                // Return relative or absolute based on input
                if (absolute)
                {
                    results.Add(fullPath);
                }
                else
                {
                    results.Add(GetRelativePath(fullPath));
                }
            }
        }
    }

    private void ExpandGlobRecursive(string basePath, string[] patternParts, int partIndex, List<string> results, bool absolute)
    {
        if (partIndex >= patternParts.Length)
            return;

        var pattern = patternParts[partIndex];
        var isLast = partIndex == patternParts.Length - 1;

        if (!_fs.IsDirectory(basePath))
            return;

        var regex = GlobToRegex(pattern);

        foreach (var name in _fs.ListDirectory(basePath))
        {
            if (regex.IsMatch(name))
            {
                var fullPath = basePath == "/" ? "/" + name : basePath + "/" + name;

                if (isLast)
                {
                    if (absolute)
                    {
                        results.Add(fullPath);
                    }
                    else
                    {
                        results.Add(GetRelativePath(fullPath));
                    }
                }
                else if (_fs.IsDirectory(fullPath))
                {
                    ExpandGlobRecursive(fullPath, patternParts, partIndex + 1, results, absolute);
                }
            }
        }
    }

    private string GetRelativePath(string absolutePath)
    {
        if (_currentDirectory == "/")
        {
            return absolutePath.TrimStart('/');
        }

        if (absolutePath.StartsWith(_currentDirectory + "/"))
        {
            return absolutePath[(_currentDirectory.Length + 1)..];
        }

        return absolutePath;
    }

    private static Regex GlobToRegex(string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".")
            .Replace("\\[", "[")
            .Replace("\\]", "]") + "$";
        return new Regex(regexPattern, RegexOptions.Compiled);
    }

    /// <summary>
    /// Finds an operator outside of quoted strings.
    /// </summary>
    private int FindUnquotedOperator(string commandLine, string op)
    {
        var inQuote = false;
        var quoteChar = '\0';

        for (int i = 0; i <= commandLine.Length - op.Length; i++)
        {
            var c = commandLine[i];

            if (inQuote)
            {
                if (c == quoteChar)
                {
                    inQuote = false;
                }
            }
            else if (c == '"' || c == '\'')
            {
                inQuote = true;
                quoteChar = c;
            }
            else if (commandLine.Substring(i, op.Length) == op)
            {
                // For single >, make sure it's not part of >>
                if (op == ">" && i + 1 < commandLine.Length && commandLine[i + 1] == '>')
                {
                    continue;
                }
                return i;
            }
        }

        return -1;
    }

    private string ExpandVariables(string text)
    {
        // First handle special parameters: $@, $*, $#, $0-$9
        text = Regex.Replace(text, @"\$([0-9@#*])", m =>
        {
            var varName = m.Groups[1].Value;
            return _environment.TryGetValue(varName, out var value) ? value : "";
        });
        
        // Then handle named variables: $VAR, $HOME, etc.
        return Regex.Replace(text, @"\$(\w+)", m =>
        {
            var varName = m.Groups[1].Value;
            return _environment.TryGetValue(varName, out var value) ? value : "";
        });
    }

    #region Commands

    private ShellResult CmdPwd(string[] args)
    {
        return ShellResult.Ok(_currentDirectory);
    }

    private ShellResult CmdCd(string[] args)
    {
        var target = args.Length > 0 ? args[0] : _environment.GetValueOrDefault("HOME", "/");
        
        if (target == "-")
        {
            target = _environment.GetValueOrDefault("OLDPWD", _currentDirectory);
        }

        var path = ResolvePath(target);
        
        if (!_fs.Exists(path))
            return ShellResult.Error($"cd: {target}: No such file or directory");
        
        if (!_fs.IsDirectory(path))
            return ShellResult.Error($"cd: {target}: Not a directory");

        _environment["OLDPWD"] = _currentDirectory;
        _currentDirectory = path;
        _environment["PWD"] = _currentDirectory;
        
        return ShellResult.Ok();
    }

    private ShellResult CmdLs(string[] args)
    {
        var showAll = args.Contains("-a") || args.Contains("-la") || args.Contains("-al") || 
                      args.Contains("-laR") || args.Contains("-lRa") || args.Contains("-aR") || 
                      args.Contains("-Ra") || args.Contains("-alR") || args.Contains("-Rla");
        var longFormat = args.Contains("-l") || args.Contains("-la") || args.Contains("-al") ||
                         args.Contains("-laR") || args.Contains("-lRa") || args.Contains("-lR") ||
                         args.Contains("-Rl") || args.Contains("-alR") || args.Contains("-Rla");
        var recursive = args.Contains("-R") || args.Contains("-laR") || args.Contains("-lRa") ||
                        args.Contains("-aR") || args.Contains("-Ra") || args.Contains("-lR") ||
                        args.Contains("-Rl") || args.Contains("-alR") || args.Contains("-Rla");
        var paths = args.Where(a => !a.StartsWith('-')).ToList();
        
        if (paths.Count == 0) paths.Add(".");

        var output = new StringBuilder();
        
        foreach (var p in paths)
        {
            var path = ResolvePath(p);
            
            if (!_fs.Exists(path))
            {
                return ShellResult.Error($"ls: cannot access '{p}': No such file or directory");
            }

            if (recursive)
            {
                LsRecursive(path, p, showAll, longFormat, output, paths.Count > 1);
            }
            else if (_fs.IsDirectory(path))
            {
                if (paths.Count > 1)
                {
                    output.AppendLine($"{p}:");
                }
                LsDirectory(path, showAll, longFormat, output);
            }
            else
            {
                output.AppendLine(FileSystemPath.GetName(path));
            }
        }

        return ShellResult.Ok(output.ToString().TrimEnd());
    }

    private void LsDirectory(string path, bool showAll, bool longFormat, StringBuilder output)
    {
        var entries = _fs.ListDirectory(path).ToList();
        
        if (!showAll)
        {
            entries = entries.Where(e => !e.StartsWith('.')).ToList();
        }

        if (longFormat)
        {
            foreach (var entry in entries)
            {
                var fullPath = path == "/" ? "/" + entry : path + "/" + entry;
                var node = _fs.GetEntry(fullPath);
                if (node != null)
                {
                    var type = node.IsDirectory ? "d" : "-";
                    var size = node.IsDirectory ? 0 : node.Content.Length;
                    output.AppendLine($"{type}rw-r--r--  {size,8}  {node.ModifiedAt:MMM dd HH:mm}  {entry}");
                }
            }
        }
        else
        {
            output.AppendLine(string.Join("  ", entries));
        }
    }

    private void LsRecursive(string path, string displayPath, bool showAll, bool longFormat, StringBuilder output, bool showHeader)
    {
        if (!_fs.IsDirectory(path))
        {
            output.AppendLine(displayPath);
            return;
        }

        output.AppendLine($"{displayPath}:");
        LsDirectory(path, showAll, longFormat, output);

        var entries = _fs.ListDirectory(path).ToList();
        if (!showAll)
        {
            entries = entries.Where(e => !e.StartsWith('.')).ToList();
        }

        foreach (var entry in entries)
        {
            var fullPath = path == "/" ? "/" + entry : path + "/" + entry;
            if (_fs.IsDirectory(fullPath))
            {
                output.AppendLine();
                var childDisplayPath = displayPath == "." ? entry : $"{displayPath}/{entry}";
                LsRecursive(fullPath, childDisplayPath, showAll, longFormat, output, true);
            }
        }
    }

    private ShellResult CmdCat(string[] args)
    {
        if (args.Length == 0)
            return ShellResult.Error("cat: missing operand");

        var output = new StringBuilder();
        
        foreach (var arg in args)
        {
            var path = ResolvePath(arg);
            
            if (!_fs.Exists(path))
                return ShellResult.Error($"cat: {arg}: No such file or directory");
            
            if (_fs.IsDirectory(path))
                return ShellResult.Error($"cat: {arg}: Is a directory");

            output.Append(_fs.ReadFile(path, Encoding.UTF8));
        }

        return ShellResult.Ok(output.ToString());
    }

    private ShellResult CmdEcho(string[] args)
    {
        return ShellResult.Ok(string.Join(" ", args));
    }

    private ShellResult CmdMkdir(string[] args)
    {
        var createParents = args.Contains("-p");
        var paths = args.Where(a => !a.StartsWith('-')).ToList();

        if (paths.Count == 0)
            return ShellResult.Error("mkdir: missing operand");

        foreach (var p in paths)
        {
            var path = ResolvePath(p);
            
            if (_fs.Exists(path))
            {
                if (!createParents)
                    return ShellResult.Error($"mkdir: cannot create directory '{p}': File exists");
                continue;
            }

            _fs.CreateDirectory(path);
        }

        return ShellResult.Ok();
    }

    private ShellResult CmdRm(string[] args)
    {
        var recursive = args.Contains("-r") || args.Contains("-rf") || args.Contains("-R");
        var force = args.Contains("-f") || args.Contains("-rf");
        var paths = args.Where(a => !a.StartsWith('-')).ToList();

        if (paths.Count == 0)
            return ShellResult.Error("rm: missing operand");

        foreach (var p in paths)
        {
            var path = ResolvePath(p);
            
            if (!_fs.Exists(path))
            {
                if (!force)
                    return ShellResult.Error($"rm: cannot remove '{p}': No such file or directory");
                continue;
            }

            try
            {
                _fs.Delete(path, recursive);
            }
            catch (InvalidOperationException ex)
            {
                return ShellResult.Error($"rm: {ex.Message}");
            }
        }

        return ShellResult.Ok();
    }

    private ShellResult CmdCp(string[] args)
    {
        var recursive = args.Contains("-r") || args.Contains("-R");
        var paths = args.Where(a => !a.StartsWith('-')).ToList();
        
        if (paths.Count < 2)
            return ShellResult.Error("cp: missing destination file operand");

        var dest = ResolvePath(paths[^1]);
        var sources = paths.Take(paths.Count - 1).ToList();

        foreach (var src in sources)
        {
            var srcPath = ResolvePath(src);
            
            if (!_fs.Exists(srcPath))
                return ShellResult.Error($"cp: cannot stat '{src}': No such file or directory");

            if (_fs.IsDirectory(srcPath) && !recursive)
                return ShellResult.Error($"cp: -r not specified; omitting directory '{src}'");

            var targetPath = _fs.IsDirectory(dest) 
                ? dest + "/" + FileSystemPath.GetName(srcPath) 
                : dest;

            _fs.Copy(srcPath, targetPath);
        }

        return ShellResult.Ok();
    }

    private ShellResult CmdMv(string[] args)
    {
        var paths = args.Where(a => !a.StartsWith('-')).ToList();
        
        if (paths.Count < 2)
            return ShellResult.Error("mv: missing destination file operand");

        var dest = ResolvePath(paths[^1]);
        var sources = paths.Take(paths.Count - 1).ToList();

        foreach (var src in sources)
        {
            var srcPath = ResolvePath(src);
            
            if (!_fs.Exists(srcPath))
                return ShellResult.Error($"mv: cannot stat '{src}': No such file or directory");

            var targetPath = _fs.IsDirectory(dest) 
                ? dest + "/" + FileSystemPath.GetName(srcPath) 
                : dest;

            _fs.Move(srcPath, targetPath);
        }

        return ShellResult.Ok();
    }

    private ShellResult CmdTouch(string[] args)
    {
        if (args.Length == 0)
            return ShellResult.Error("touch: missing file operand");

        foreach (var arg in args.Where(a => !a.StartsWith('-')))
        {
            var path = ResolvePath(arg);
            
            if (!_fs.Exists(path))
            {
                _fs.WriteFile(path, Array.Empty<byte>());
            }
            else
            {
                var entry = _fs.GetEntry(path);
                if (entry != null) entry.ModifiedAt = DateTime.UtcNow;
            }
        }

        return ShellResult.Ok();
    }

    private ShellResult CmdHead(string[] args)
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
            return ShellResult.Error("head: missing file operand");

        var output = new StringBuilder();
        foreach (var p in paths)
        {
            var path = ResolvePath(p);
            var content = _fs.ReadFile(path, System.Text.Encoding.UTF8);
            var fileLines = content.Split('\n').Take(lines);
            output.AppendLine(string.Join("\n", fileLines));
        }

        return ShellResult.Ok(output.ToString().TrimEnd());
    }

    private ShellResult CmdTail(string[] args)
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
            var path = ResolvePath(p);
            var content = _fs.ReadFile(path, System.Text.Encoding.UTF8);
            var allLines = content.Split('\n');
            var fileLines = allLines.Skip(Math.Max(0, allLines.Length - lines));
            output.AppendLine(string.Join("\n", fileLines));
        }

        return ShellResult.Ok(output.ToString().TrimEnd());
    }

    private ShellResult CmdWc(string[] args)
    {
        var paths = args.Where(a => !a.StartsWith('-')).ToList();
        
        if (paths.Count == 0)
            return ShellResult.Error("wc: missing file operand");

        var output = new StringBuilder();
        long totalLines = 0, totalWords = 0, totalBytes = 0;

        foreach (var p in paths)
        {
            var path = ResolvePath(p);
            var content = _fs.ReadFile(path, System.Text.Encoding.UTF8);
            var bytes = _fs.ReadFile(path, System.Text.Encoding.UTF8);
            
            var lines = content.Count(c => c == '\n');
            var words = content.Split(new[] { ' ', '\n', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            
            output.AppendLine($"  {lines,6}  {words,6}  {bytes.Length,6} {p}");
            totalLines += lines;
            totalWords += words;
            totalBytes += bytes.Length;
        }

        if (paths.Count > 1)
        {
            output.AppendLine($"  {totalLines,6}  {totalWords,6}  {totalBytes,6} total");
        }

        return ShellResult.Ok(output.ToString().TrimEnd());
    }

    private ShellResult CmdGrep(string[] args)
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
            var path = ResolvePath(p);
            if (_fs.IsDirectory(path))
            {
                if (recursive)
                {
                    CollectFilesRecursive(path, p, filePaths);
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
                var content = _fs.ReadFile(fullPath, System.Text.Encoding.UTF8);
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

    private void CollectFilesRecursive(string path, string displayPath, List<(string DisplayPath, string FullPath)> files)
    {
        if (!_fs.IsDirectory(path))
        {
            files.Add((displayPath, path));
            return;
        }

        foreach (var entry in _fs.ListDirectory(path))
        {
            var fullPath = path == "/" ? "/" + entry : path + "/" + entry;
            var childDisplayPath = displayPath == "." ? entry : $"{displayPath}/{entry}";

            if (_fs.IsDirectory(fullPath))
            {
                CollectFilesRecursive(fullPath, childDisplayPath, files);
            }
            else
            {
                files.Add((childDisplayPath, fullPath));
            }
        }
    }

    private ShellResult CmdFind(string[] args)
    {
        var startPath = args.Length > 0 && !args[0].StartsWith('-') ? args[0] : ".";
        var namePattern = "*";
        
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-name")
            {
                namePattern = args[i + 1];
            }
        }

        var basePath = ResolvePath(startPath);
        var output = new StringBuilder();
        
        FindRecursive(basePath, namePattern, output);

        return ShellResult.Ok(output.ToString().TrimEnd());
    }

    private void FindRecursive(string path, string pattern, StringBuilder output)
    {
        var name = FileSystemPath.GetName(path);
        var regex = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$");
        
        if (regex.IsMatch(name) || pattern == "*")
        {
            output.AppendLine(path);
        }

        if (_fs.IsDirectory(path))
        {
            foreach (var child in _fs.ListDirectory(path))
            {
                var childPath = path == "/" ? "/" + child : path + "/" + child;
                FindRecursive(childPath, pattern, output);
            }
        }
    }

    private ShellResult CmdEnv(string[] args)
    {
        var output = new StringBuilder();
        foreach (var kvp in _environment.OrderBy(k => k.Key))
        {
            output.AppendLine($"{kvp.Key}={kvp.Value}");
        }
        return ShellResult.Ok(output.ToString().TrimEnd());
    }

    private ShellResult CmdExport(string[] args)
    {
        foreach (var arg in args)
        {
            var parts = arg.Split('=', 2);
            if (parts.Length == 2)
            {
                _environment[parts[0]] = parts[1];
            }
        }
        return ShellResult.Ok();
    }

    private ShellResult CmdSh(string[] args)
    {
        if (args.Length == 0)
        {
            return ShellResult.Error("sh: missing script path\nUsage: sh <script.sh> [args...]");
        }

        var scriptPath = ResolvePath(args[0]);

        if (!_fs.Exists(scriptPath) || !_fs.IsFile(scriptPath))
        {
            return ShellResult.Error($"sh: {args[0]}: No such file");
        }

        var scriptContent = _fs.ReadFile(scriptPath, Encoding.UTF8);
        var scriptArgs = args.Skip(1).ToArray();

        return ExecuteScript(scriptContent, scriptArgs);
    }

    /// <summary>
    /// Executes a shell script with the given arguments.
    /// </summary>
    private ShellResult ExecuteScript(string script, string[] args)
    {
        // Save current environment state for positional parameters
        var savedParams = new Dictionary<string, string?>();
        var paramNames = new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "@", "#", "*" };
        foreach (var name in paramNames)
        {
            savedParams[name] = _environment.TryGetValue(name, out var val) ? val : null;
        }

        try
        {
            // Set positional parameters $1, $2, etc.
            for (int i = 0; i < args.Length && i < 9; i++)
            {
                _environment[$"{i + 1}"] = args[i];
            }
            // Clear unused positional parameters
            for (int i = args.Length; i < 9; i++)
            {
                _environment.Remove($"{i + 1}");
            }
            
            _environment["@"] = string.Join(" ", args);
            _environment["*"] = string.Join(" ", args);
            _environment["#"] = args.Length.ToString();

            var lines = script.Split('\n');
            var output = new StringBuilder();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                    continue;

                var result = Execute(trimmed);

                if (!string.IsNullOrEmpty(result.Stdout))
                {
                    if (output.Length > 0)
                        output.AppendLine();
                    output.Append(result.Stdout);
                }

                // Stop on error (set -e behavior)
                if (result.ExitCode != 0)
                {
                    return new ShellResult
                    {
                        ExitCode = result.ExitCode,
                        Stdout = output.ToString(),
                        Stderr = result.Stderr
                    };
                }
            }

            return ShellResult.Ok(output.ToString());
        }
        finally
        {
            // Restore positional parameters
            foreach (var (name, value) in savedParams)
            {
                if (value != null)
                    _environment[name] = value;
                else
                    _environment.Remove(name);
            }
        }
    }

    private ShellResult CmdHelp(string[] args)
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
        return ShellResult.Ok(output.ToString().TrimEnd());
    }

    #endregion
}
