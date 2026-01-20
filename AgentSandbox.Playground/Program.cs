using AgentSandbox.Core;
using AgentSandbox.Core.Shell;
using AgentSandbox.Core.Shell.Extensions;
using AgentSandbox.Core.Skills;
using AgentSandbox.Core.Telemetry;
using AgentSandbox.Playground;

// Get the skills folder path relative to the executable
var skillsPath = Path.Combine(AppContext.BaseDirectory, "Skills");

Sandbox sandbox = new Sandbox(options: new SandboxOptions
{
    ShellExtensions = new List<IShellCommand>
    {
        new CurlCommand(),
        new JqCommand(),
        new GitCommand()
    },
    AgentSkills = new AgentSkillOptions
    {
        Skills = 
        [
            AgentSkill.FromPath(Path.Combine(skillsPath, "brainstorming")),
            AgentSkill.FromPath(Path.Combine(skillsPath, "executing-plans"))
        ]
    },
    Telemetry = new SandboxTelemetryOptions
    {
        Enabled = true
    }
});

// Subscribe console telemetry observer for real-time event monitoring
var consoleObserver = new ConsoleTelemetryObserver(new ConsoleTelemetryOptions
{
    ShowCommands = true,
    ShowCommandDetails = false,  // Set to true for verbose output
    ShowFileChanges = true,
    ShowSkills = true,
    ShowLifecycle = true,
    ShowErrors = true
});

using var subscription = sandbox.Subscribe(consoleObserver);

Console.WriteLine("=== AgentSandbox Playground ===");
Console.WriteLine("Telemetry monitoring enabled. Type 'exit' to quit.\n");

while (true)
{
    Console.Write("SandboxShell > ");

    string? command = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(command))
        continue;

    if (command.Trim().ToLower() == "exit")
        break;

    var result = sandbox.Execute(command);

    Console.WriteLine("================================");

    if (result != null)
    {
        if (result.Success)
        {
            Console.WriteLine("Output: " + result.Stdout);
        }
        else
        {
            Console.WriteLine("Error: " + result.Stderr);
        }
    }

    var stats = sandbox.GetStats();

    Console.WriteLine("================================");
    Console.WriteLine("Stats: " + stats.CommandCount + " commands executed, " + stats.FileCount + " files created.");
}