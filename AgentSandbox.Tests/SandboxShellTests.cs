using AgentSandbox.Core.FileSystem;
using AgentSandbox.Core.Shell;

namespace AgentSandbox.Tests;

public class SandboxShellTests
{
    private readonly FileSystem _fs;
    private readonly SandboxShell _shell;

    public SandboxShellTests()
    {
        _fs = new FileSystem();
        _shell = new SandboxShell(_fs);
    }

    [Fact]
    public void Pwd_ReturnsCurrentDirectory()
    {
        var result = _shell.Execute("pwd");
        
        Assert.True(result.Success);
        Assert.Equal("/", result.Stdout);
    }

    [Fact]
    public void Cd_ChangesDirectory()
    {
        _fs.CreateDirectory("/mydir");
        
        var result = _shell.Execute("cd /mydir");
        
        Assert.True(result.Success);
        Assert.Equal("/mydir", _shell.CurrentDirectory);
    }

    [Fact]
    public void Cd_FailsForNonexistentDirectory()
    {
        var result = _shell.Execute("cd /nonexistent");
        
        Assert.False(result.Success);
        Assert.Contains("No such file or directory", result.Stderr);
    }

    [Fact]
    public void Mkdir_CreatesDirectory()
    {
        var result = _shell.Execute("mkdir /newdir");
        
        Assert.True(result.Success);
        Assert.True(_fs.Exists("/newdir"));
        Assert.True(_fs.IsDirectory("/newdir"));
    }

    [Fact]
    public void Mkdir_WithP_CreatesParents()
    {
        var result = _shell.Execute("mkdir -p /a/b/c");
        
        Assert.True(result.Success);
        Assert.True(_fs.Exists("/a/b/c"));
    }

    [Fact]
    public void Touch_CreatesEmptyFile()
    {
        var result = _shell.Execute("touch /newfile.txt");
        
        Assert.True(result.Success);
        Assert.True(_fs.Exists("/newfile.txt"));
        Assert.Equal(0, _fs.ReadFile("/newfile.txt", System.Text.Encoding.UTF8).Length);
    }

    [Fact]
    public void Echo_PrintsText()
    {
        var result = _shell.Execute("echo Hello World");
        
        Assert.True(result.Success);
        Assert.Equal("Hello World", result.Stdout);
    }

    [Fact]
    public void Cat_PrintsFileContent()
    {
        _fs.WriteFile("/test.txt", "file content");
        
        var result = _shell.Execute("cat /test.txt");
        
        Assert.True(result.Success);
        Assert.Equal("file content", result.Stdout);
    }

    [Fact]
    public void Ls_ListsDirectoryContents()
    {
        _fs.WriteFile("/a.txt", "a");
        _fs.WriteFile("/b.txt", "b");
        
        var result = _shell.Execute("ls /");
        
        Assert.True(result.Success);
        Assert.Contains("a.txt", result.Stdout);
        Assert.Contains("b.txt", result.Stdout);
    }

    [Fact]
    public void Rm_RemovesFile()
    {
        _fs.WriteFile("/delete.txt", "x");
        
        var result = _shell.Execute("rm /delete.txt");
        
        Assert.True(result.Success);
        Assert.False(_fs.Exists("/delete.txt"));
    }

    [Fact]
    public void Rm_Rf_RemovesDirectoryRecursively()
    {
        _fs.WriteFile("/dir/sub/file.txt", "x");
        
        var result = _shell.Execute("rm -rf /dir");
        
        Assert.True(result.Success);
        Assert.False(_fs.Exists("/dir"));
    }

    [Fact]
    public void Cp_CopiesFile()
    {
        _fs.WriteFile("/source.txt", "content");
        
        var result = _shell.Execute("cp /source.txt /dest.txt");
        
        Assert.True(result.Success);
        Assert.True(_fs.Exists("/dest.txt"));
        Assert.Equal("content", _fs.ReadFile("/dest.txt", System.Text.Encoding.UTF8));
    }

    [Fact]
    public void Mv_MovesFile()
    {
        _fs.WriteFile("/old.txt", "content");
        
        var result = _shell.Execute("mv /old.txt /new.txt");
        
        Assert.True(result.Success);
        Assert.False(_fs.Exists("/old.txt"));
        Assert.True(_fs.Exists("/new.txt"));
    }

    [Fact]
    public void Head_ShowsFirstLines()
    {
        _fs.WriteFile("/lines.txt", "line1\nline2\nline3\nline4\nline5");
        
        var result = _shell.Execute("head -n 2 /lines.txt");
        
        Assert.True(result.Success);
        Assert.Contains("line1", result.Stdout);
        Assert.Contains("line2", result.Stdout);
        Assert.DoesNotContain("line3", result.Stdout);
    }

    [Fact]
    public void Tail_ShowsLastLines()
    {
        _fs.WriteFile("/lines.txt", "line1\nline2\nline3\nline4\nline5");
        
        var result = _shell.Execute("tail -n 2 /lines.txt");
        
        Assert.True(result.Success);
        Assert.Contains("line4", result.Stdout);
        Assert.Contains("line5", result.Stdout);
        Assert.DoesNotContain("line1", result.Stdout);
    }

    [Fact]
    public void Grep_FindsMatchingLines()
    {
        _fs.WriteFile("/search.txt", "apple\nbanana\napricot\ncherry");
        
        var result = _shell.Execute("grep ap /search.txt");
        
        Assert.True(result.Success);
        Assert.Contains("apple", result.Stdout);
        Assert.Contains("apricot", result.Stdout);
        Assert.DoesNotContain("banana", result.Stdout);
    }

    [Fact]
    public void Export_SetsEnvironmentVariable()
    {
        _shell.Execute("export MY_VAR=my_value");
        
        Assert.Equal("my_value", _shell.Environment["MY_VAR"]);
    }

    [Fact]
    public void VariableExpansion_Works()
    {
        _shell.Execute("export NAME=World");
        
        var result = _shell.Execute("echo Hello $NAME");
        
        Assert.Equal("Hello World", result.Stdout);
    }

    [Fact]
    public void RelativePaths_Work()
    {
        _fs.CreateDirectory("/home/user");
        _shell.Execute("cd /home/user");
        _shell.Execute("mkdir mydir");
        _shell.Execute("touch mydir/file.txt");
        
        Assert.True(_fs.Exists("/home/user/mydir"));
        Assert.True(_fs.Exists("/home/user/mydir/file.txt"));
    }

    [Fact]
    public void UnknownCommand_ReturnsError()
    {
        var result = _shell.Execute("unknowncommand");
        
        Assert.False(result.Success);
        Assert.Equal(127, result.ExitCode);
        Assert.Contains("command not found", result.Stderr);
    }

    [Fact]
    public void Help_ListsAvailableCommands()
    {
        var result = _shell.Execute("help");

        Assert.True(result.Success);
        Assert.Contains("pwd", result.Stdout);
        Assert.Contains("cd", result.Stdout);
        Assert.Contains("ls", result.Stdout);
        Assert.Contains("sh", result.Stdout);
    }

    #region sh Command Tests

    [Fact]
    public void Sh_ExecutesSimpleScript()
    {
        _fs.WriteFile("/script.sh", "echo Hello\necho World");
        
        var result = _shell.Execute("sh /script.sh");
        
        Assert.True(result.Success);
        Assert.Contains("Hello", result.Stdout);
        Assert.Contains("World", result.Stdout);
    }

    [Fact]
    public void Sh_SkipsComments()
    {
        _fs.WriteFile("/script.sh", "# This is a comment\necho Hello\n# Another comment");
        
        var result = _shell.Execute("sh /script.sh");
        
        Assert.True(result.Success);
        Assert.Equal("Hello", result.Stdout);
    }

    [Fact]
    public void Sh_SkipsShebang()
    {
        _fs.WriteFile("/script.sh", "#!/bin/bash\necho Hello");
        
        var result = _shell.Execute("sh /script.sh");
        
        Assert.True(result.Success);
        Assert.Equal("Hello", result.Stdout);
    }

    [Fact]
    public void Sh_SupportsPositionalParameters()
    {
        _fs.WriteFile("/script.sh", "echo $1 $2");
        
        var result = _shell.Execute("sh /script.sh foo bar");
        
        Assert.True(result.Success);
        Assert.Equal("foo bar", result.Stdout);
    }

    [Fact]
    public void Sh_SupportsAllArgsParameter()
    {
        _fs.WriteFile("/script.sh", "echo $@");
        
        var result = _shell.Execute("sh /script.sh one two three");
        
        Assert.True(result.Success);
        Assert.Equal("one two three", result.Stdout);
    }

    [Fact]
    public void Sh_SupportsArgCountParameter()
    {
        _fs.WriteFile("/script.sh", "echo $#");
        
        var result = _shell.Execute("sh /script.sh a b c");
        
        Assert.True(result.Success);
        Assert.Equal("3", result.Stdout);
    }

    [Fact]
    public void Sh_StopsOnError()
    {
        _fs.WriteFile("/script.sh", "echo First\ncat /nonexistent\necho Second");
        
        var result = _shell.Execute("sh /script.sh");
        
        Assert.False(result.Success);
        Assert.Contains("First", result.Stdout);
        Assert.DoesNotContain("Second", result.Stdout);
    }

    [Fact]
    public void Sh_FileNotFound()
    {
        var result = _shell.Execute("sh /nonexistent.sh");
        
        Assert.False(result.Success);
        Assert.Contains("No such file", result.Stderr);
    }

    [Fact]
    public void Sh_MissingPath()
    {
        var result = _shell.Execute("sh");
        
        Assert.False(result.Success);
        Assert.Contains("missing script path", result.Stderr);
    }

    [Fact]
    public void Sh_ExecutesFileOperations()
    {
        _fs.WriteFile("/script.sh", "mkdir /testdir\ntouch /testdir/file.txt\necho done");
        
        var result = _shell.Execute("sh /script.sh");
        
        Assert.True(result.Success);
        Assert.True(_fs.Exists("/testdir"));
        Assert.True(_fs.Exists("/testdir/file.txt"));
    }

    [Fact]
    public void Sh_DirectExecution_WithDotSlash()
    {
        _fs.WriteFile("/script.sh", "echo Direct");
        
        var result = _shell.Execute("./script.sh");
        
        Assert.True(result.Success);
        Assert.Equal("Direct", result.Stdout);
    }

    [Fact]
    public void Sh_DirectExecution_WithAbsolutePath()
    {
        _fs.CreateDirectory("/scripts");
        _fs.WriteFile("/scripts/test.sh", "echo Absolute");
        
        var result = _shell.Execute("/scripts/test.sh");
        
        Assert.True(result.Success);
        Assert.Equal("Absolute", result.Stdout);
    }

    [Fact]
    public void Sh_DirectExecution_WithArgs()
    {
        _fs.WriteFile("/script.sh", "echo $1");
        
        var result = _shell.Execute("./script.sh myarg");
        
        Assert.True(result.Success);
        Assert.Equal("myarg", result.Stdout);
    }

    [Fact]
    public void Sh_RestoresEnvironmentAfterExecution()
    {
        _shell.Execute("export VAR=original");
        _fs.WriteFile("/script.sh", "export VAR=modified\necho $1");
        
        _shell.Execute("sh /script.sh test");
        
        // Check that positional parameters are restored (not leaking from script)
        var result = _shell.Execute("echo $1");
        Assert.Equal("", result.Stdout.Trim());
    }

    [Fact]
    public void Sh_HandlesEmptyLines()
    {
        _fs.WriteFile("/script.sh", "echo First\n\n\necho Second");
        
        var result = _shell.Execute("sh /script.sh");
        
        Assert.True(result.Success);
        Assert.Contains("First", result.Stdout);
        Assert.Contains("Second", result.Stdout);
    }

    [Fact]
    public void Sh_ExecutesNestedScript()
    {
        _fs.WriteFile("/outer.sh", "echo Outer\nsh /inner.sh\necho Done");
        _fs.WriteFile("/inner.sh", "echo Inner");
        
        var result = _shell.Execute("sh /outer.sh");
        
        Assert.True(result.Success);
        Assert.Contains("Outer", result.Stdout);
        Assert.Contains("Inner", result.Stdout);
        Assert.Contains("Done", result.Stdout);
    }

    #endregion
}
