using Fedele.Tests.Shared;

namespace Fedele.Cli.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void FedeleCli_References_NoTools()
    {
        var references = AssemblyReferences.FedeleReferencesOf("fedele");

        Assert.DoesNotContain("Fedele.Anonymizer", references);
    }
}
