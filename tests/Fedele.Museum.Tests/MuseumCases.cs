using System.Text.RegularExpressions;

namespace Fedele.Museum.Tests;

/// <summary>Finds the museum cases: every folder of museum/ is a case, no code needed to add one.</summary>
internal static partial class MuseumCases
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string MuseumDirectory => Path.Combine(RepositoryRoot, "museum");

    /// <summary>Case folder names, sorted.</summary>
    public static IReadOnlyList<string> Names() =>
        [.. Directory.EnumerateDirectories(MuseumDirectory)
            .Select(path => Path.GetFileName(path))
            .Order(StringComparer.Ordinal)];

    public static TheoryData<string> TheoryData()
    {
        var data = new TheoryData<string>();
        foreach (var name in Names())
        {
            data.Add(name);
        }

        return data;
    }

    public static string CaseDirectory(string name) => Path.Combine(MuseumDirectory, name);

    /// <summary>The single input.* file of a case.</summary>
    public static string InputPath(string name) =>
        Directory.EnumerateFiles(CaseDirectory(name), "input.*").Single();

    public static ExpectedCase Expected(string name) =>
        ExpectedCase.Parse(File.ReadAllText(Path.Combine(CaseDirectory(name), "expected.json")));

    /// <summary>NNNN-short-name, lowercase words separated by hyphens.</summary>
    [GeneratedRegex("^[0-9]{4}-[a-z0-9]+(-[a-z0-9]+)*$")]
    public static partial Regex CaseNameRegex();

    // Walks up from the test output folder until it finds the solution file.
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Fedele.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root (Fedele.slnx) not found.");
    }
}
