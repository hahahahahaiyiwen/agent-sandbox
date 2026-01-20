namespace AgentSandbox.Core.Mounting;

/// <summary>
/// Represents a file mount configuration: source files and destination path.
/// </summary>
public class FileMountOptions
{
    /// <summary>
    /// The destination path in the sandbox where files will be mounted.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The source of files to mount.
    /// </summary>
    public required IFileSource Source { get; init; }
}