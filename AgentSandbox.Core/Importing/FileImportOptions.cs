namespace AgentSandbox.Core.Importing;

/// <summary>
/// Represents a file import configuration: source files and destination path.
/// </summary>
public class FileImportOptions
{
    /// <summary>
    /// The destination path in the sandbox where files will be copied.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The source of files to import.
    /// </summary>
    public required IFileSource Source { get; init; }
}
