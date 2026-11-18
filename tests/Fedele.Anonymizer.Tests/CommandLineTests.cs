namespace Fedele.Anonymizer.Tests;

public sealed class CommandLineTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("fedele-anonymizer-tests-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Run_NoArguments_PrintsUsage()
    {
        var (exitCode, output, _) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output);
    }

    [Fact]
    public void Run_UnknownCommand_ReturnsUsageError()
    {
        Assert.Equal(1, Run("explode").ExitCode);
    }

    [Fact]
    public void Anonymize_WithoutColumnsOption_IsRefused()
    {
        var input = WriteInput("a;b\n1;2\n");

        var (exitCode, _, error) = Run("anonymize", input, Path.Combine(_directory, "out.csv"));

        Assert.Equal(1, exitCode);
        Assert.Contains("--columns is required", error);
    }

    [Fact]
    public void Anonymize_ColumnsNone_WritesVerifiedIdenticalCopy()
    {
        var input = WriteInput("a;b\r\n1;\"x\r\ny\"\r\n");
        var output = Path.Combine(_directory, "museum", "0001-case", "input.csv");

        var (exitCode, text, _) = Run("anonymize", input, output, "--columns", "none");

        Assert.Equal(0, exitCode);
        Assert.Contains("Byte-identical copy: yes", text);
        Assert.Equal(File.ReadAllBytes(input), File.ReadAllBytes(output));
    }

    [Fact]
    public void Anonymize_ExistingOutput_IsNotOverwritten()
    {
        var input = WriteInput("a;b\n1;2\n");
        var output = Path.Combine(_directory, "out.csv");
        File.WriteAllText(output, "keep me");

        var (exitCode, _, _) = Run("anonymize", input, output, "--columns", "none");

        Assert.Equal(1, exitCode);
        Assert.Equal("keep me", File.ReadAllText(output));
    }

    [Fact]
    public void Anonymize_ColumnByName_ReplacesOnlyThatColumn()
    {
        var input = WriteInput("Codice;Nome\n001;Mario\n002;Anna\n");
        var output = Path.Combine(_directory, "out.csv");

        var (exitCode, text, _) = Run("anonymize", input, output, "--columns", "nome", "--seed", "3");

        Assert.Equal(0, exitCode);
        Assert.Contains("Column   2 : 2 values replaced", text);
        var lines = File.ReadAllLines(output);
        Assert.Equal("Codice;Nome", lines[0]);
        Assert.Matches("^001;[A-Z][a-z]{4}$", lines[1]);
    }

    [Fact]
    public void Anonymize_UnknownColumn_IsRefused()
    {
        var input = WriteInput("Codice;Nome\n001;Mario\n");

        var (exitCode, _, error) = Run("anonymize", input, Path.Combine(_directory, "out.csv"), "--columns", "Cognome");

        Assert.Equal(1, exitCode);
        Assert.Contains("Unknown column: Cognome", error);
    }

    [Fact]
    public void Analyze_Folder_ReportsEveryFileWithoutValues()
    {
        WriteInput("Nome;Codice\nSENTINELONE;SENTINELTWO\n", "b.csv");
        WriteInput("x,y\n1,2\n", "a.csv");

        var (exitCode, output, _) = Run("analyze", _directory);

        Assert.Equal(0, exitCode);
        Assert.True(output.IndexOf("a.csv", StringComparison.Ordinal) < output.IndexOf("b.csv", StringComparison.Ordinal));
        Assert.DoesNotContain("SENTINEL", output);
    }

    private string WriteInput(string content, string name = "input.csv")
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, TestFiles.Utf8(content));
        return path;
    }

    private static (int ExitCode, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = CommandLine.Run(args, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }
}
