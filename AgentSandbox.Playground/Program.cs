using AgentSandbox.Core;
using AgentSandbox.Core.Shell;
using AgentSandbox.Core.Shell.Extensions;
using AgentSandbox.Core.Skills;

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
    Skills = 
    [
        AgentSkill.FromPath(Path.Combine(skillsPath, "brainstorming")),
        AgentSkill.FromPath(Path.Combine(skillsPath, "executing-plans"))
    ]
});

while (true)
{
    Console.Write("SandboxShell > ");

    string? command = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(command))
        continue;

    if (command.Trim().ToLower() == "exit")
        break;

    var result = sandbox.Execute(command);

    if (result != null)
    {
        Console.WriteLine("================================");
        Console.WriteLine("Command: " + result.Command);

        if (result.Success)
        {
            Console.WriteLine("Output: " + result.Stdout);
        }
        else
        {
            Console.WriteLine("Error: " + result.Stderr);
        }

        Console.WriteLine("Duration: " + result.Duration.TotalMilliseconds + " ms");
        Console.WriteLine("================================");
    }

    var stats = sandbox.GetStats();

    Console.WriteLine("Sandbox stats: " + stats.CommandCount + " commands executed, " + stats.FileCount + " files created.");
}