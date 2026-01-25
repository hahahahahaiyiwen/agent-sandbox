namespace AgentSandbox.Core;

/// <summary>
/// Snapshot of sandbox state for persistence/restoration.
/// </summary>
public class SandboxSnapshot
{
    public string Id { get; set; } = string.Empty;
    public byte[] FileSystemData { get; set; } = Array.Empty<byte>();
    public string CurrentDirectory { get; set; } = "/";
    public Dictionary<string, string> Environment { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
