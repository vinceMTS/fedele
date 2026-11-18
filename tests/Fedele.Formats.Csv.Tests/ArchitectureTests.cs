using Fedele.Tests.Shared;

namespace Fedele.Formats.Csv.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void FedeleFormatsCsv_References_OnlyFedeleCore()
    {
        var references = AssemblyReferences.FedeleReferencesOf("Fedele.Formats.Csv");

        Assert.All(references, name => Assert.Equal("Fedele.Core", name));
    }

    [Fact]
    public void FedeleFormatsCsv_References_NoExternalPackages()
    {
        Assert.Empty(AssemblyReferences.ExternalReferencesOf("Fedele.Formats.Csv"));
    }
}
