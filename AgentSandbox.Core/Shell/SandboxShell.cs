using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AgentSandbox.Core.FileSystem;
using AgentSandbox.Core.Shell.Commands;

namespace AgentSandbox.Core.Shell;

/// <summary>
/// A sandboxed shell that executes commands against a virtual filesystem.
/// Emulates common Unix commands without touching the real filesystem.
/// Supports extensibility via IShellCommand registration.
/// </summary>
public class SandboxShell : ISandboxShell, IShellContext, IExtendedShellContext
{
    private readonly IFileSystem _fs;
    private string _currentDirectory = "/";
    private readonly Dictionary<string, string> _environment = new();
    private readonly Dictionary<string, IShellCommand> _builtinCommands = new();
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

        // Register built-in commands
        RegisterBuiltinCommand(new PwdCommand());
        RegisterBuiltinCommand(new CdCommand());
        RegisterBuiltinCommand(new LsCommand());
        RegisterBuiltinCommand(new CatCommand());
        RegisterBuiltinCommand(new EchoCommand());
        RegisterBuiltinCommand(new MkdirCommand());
        RegisterBuiltinCommand(new RmCommand());
        RegisterBuiltinCommand(new CpCommand());
        RegisterBuiltinCommand(new MvCommand());
        RegisterBuiltinCommand(new TouchCommand());
        RegisterBuiltinCommand(new HeadCommand());
        RegisterBuiltinCommand(new TailCommand());
        RegisterBuiltinCommand(new WcCommand());
        RegisterBuiltinCommand(new GrepCommand());
        RegisterBuiltinCommand(new FindCommand());
        RegisterBuiltinCommand(new EnvCommand());
        RegisterBuiltinCommand(new ExportCommand());
        RegisterBuiltinCommand(new ClearCommand());
        RegisterBuiltinCommand(new HelpCommand());
    }

    private void RegisterBuiltinCommand(IShellCommand command)
    {
        _builtinCommands[command.Name.ToLowerInvariant()] = command;
        foreach (var alias in command.Aliases)
        {
            _builtinCommands[alias.ToLowerInvariant()] = command;
        }
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
        return _builtinCommands.Values.Select(c => c.Name).Distinct()
            .Concat(_extensionCommands.Values.Select(c => c.Name).Distinct())
            .OrderBy(c => c);
    }

    /// <summary>
    /// Gets all extension commands for the help command.
    /// </summary>
    public IEnumerable<IShellCommand> GetExtensionCommands()
    {
        return _extensionCommands.Values.Distinct();
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

        // Check for input redirection (not supported)
        var heredocIndex = FindUnquotedOperator(commandLine, "<<");
        if (heredocIndex >= 0)
        {
            return ShellResult.Error(
                "Heredoc (<<) is not supported. Workarounds:\n" +
                "  - Write content to a file first, then use file as argument\n" +
                "  - Use 'echo \"content\" > file.txt' to create input files");
        }
        
        var inputRedirectIndex = FindUnquotedOperator(commandLine, "<");
        if (inputRedirectIndex >= 0)
        {
            return ShellResult.Error(
                "Input redirection (<) is not supported. Workarounds:\n" +
                "  - Use file arguments directly: 'cat file.txt' instead of 'cat < file.txt'\n" +
                "  - Most commands accept file paths as arguments");
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
                result = ExecuteShCommand(new[] { cmd }.Concat(args).ToArray());
            }
            catch (Exception ex)
            {
                result = ShellResult.Error($"{cmd}: {ex.Message}");
            }
        }
        // Handle 'sh' command specially (needs to call Execute recursively)
        else if (cmdLower == "sh")
        {
            try
            {
                if (args.Length > 0 && args[0] == "-h")
                {
                    result = ShellResult.Ok("sh - Execute shell script\n\nUsage: sh <script.sh> [args...]");
                }
                else
                {
                    result = ExecuteShCommand(args);
                }
            }
            catch (Exception ex)
            {
                result = ShellResult.Error($"sh: {ex.Message}");
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
                    result = ShellResult.Ok($"{command.Name} - {command.Description}\n\nUsage: {command.Usage}");
                }
                else
                {
                    result = command.Execute(args, this);
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

    #region Script Execution

    /// <summary>
    /// Handles the 'sh' command - loads and executes a shell script file.
    /// </summary>
    private ShellResult ExecuteShCommand(string[] args)
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

    #endregion
}