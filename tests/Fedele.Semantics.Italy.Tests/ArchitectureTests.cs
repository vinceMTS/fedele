using Fedele.Tests.Shared;

namespace Fedele.Semantics.Italy.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void FedeleSemanticsItaly_References_OnlyFedeleCore()
    {
        var references = AssemblyReferences.FedeleReferencesOf("Fedele.Semantics.Italy");

        Assert.All(references, name => Assert.Equal("Fedele.Core", name));
    }

    [Fact]
    public void FedeleSemanticsItaly_References_NoExternalPackages()
    {
        Assert.Empty(AssemblyReferences.ExternalReferencesOf("Fedele.Semantics.Italy"));
    }
}
