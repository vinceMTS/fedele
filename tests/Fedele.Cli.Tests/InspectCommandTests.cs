using System.Text;

namespace Fedele.Cli.Tests;

public sealed class InspectCommandTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("fedele-cli-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Inspect_CsvFile_ShowsLayoutSummaryAndIssues()
    {
        var path = WriteFile("Nome;Importo\r\nRossi;1.234,56\r\n;\r\nBianchi;7,00\r\n");

        var (exitCode, output, _) = await RunAsync("inspect", path);

        Assert.Equal(CommandLine.Ok, exitCode);
        Assert.Contains("Format: csv", output, StringComparison.Ordinal);
        Assert.Matches(@"separator\s+';'\s+1\.00", output);
        Assert.Contains("  1  Nome", output, StringComparison.Ordinal);
        Assert.Matches(@"records\s+3", output);
        Assert.Contains("empty-record  record 3, line 3: Every field of the record is empty.", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_AnyFile_NeverPrintsRecordValues()
    {
        var path = WriteFile("Nome,CodiceFiscale\nMario,RSSMRA80A01H501U\n");

        var (_, output, _) = await RunAsync("inspect", path);

        Assert.Contains("CodiceFiscale", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Mario", output, StringComparison.Ordinal);
        Assert.DoesNotContain("RSSMRA80A01H501U", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_SeparatorAndNoHeaderOptions_AreUsed()
    {
        var path = WriteFile("1|2,5\n3|4,5\n");

        var (exitCode, output, _) = await RunAsync("inspect", path, "--separator", "|", "--no-header");

        Assert.Equal(CommandLine.Ok, exitCode);
        Assert.Contains("Set in the read options.", output, StringComparison.Ordinal);
        Assert.Matches(@"records\s+2", output);
        Assert.DoesNotContain("Columns", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_MissingFile_FailsWithMessage()
    {
        var (exitCode, _, error) = await RunAsync("inspect", Path.Combine(_directory, "missing.csv"));

        Assert.Equal(CommandLine.Failure, exitCode);
        Assert.Contains("File not found", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("frobnicate")]
    [InlineData("inspect")]
    [InlineData("inspect", "a.csv", "b.csv")]
    [InlineData("inspect", "a.csv", "--separator", ";;")]
    [InlineData("inspect", "a.csv", "--encoding", "latin-9")]
    [InlineData("inspect", "a.csv", "--separator")]
    public async Task Run_InvalidArguments_IsUsageError(params string[] args)
    {
        var (exitCode, _, error) = await RunAsync(args);

        Assert.Equal(CommandLine.UsageError, exitCode);
        Assert.Contains("fedele --help", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_NoArguments_PrintsUsage()
    {
        var (exitCode, output, _) = await RunAsync();

        Assert.Equal(CommandLine.Ok, exitCode);
        Assert.Contains("fedele inspect <file>", output, StringComparison.Ordinal);
    }

    private string WriteFile(string content)
    {
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".csv");
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));
        return path;
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CommandLine.RunAsync(args, output, error, TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }
}
