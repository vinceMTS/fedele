namespace Fedele.Museum.Tests;

public sealed class MuseumTests
{
    [Fact]
    public void MuseumDirectory_InRepositoryRoot_Exists()
    {
        var museum = Path.Combine(FindRepositoryRoot(), "museum");

        Assert.True(Directory.Exists(museum), $"Museum directory not found: {museum}");
    }

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
