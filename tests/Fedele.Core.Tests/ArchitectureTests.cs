using Fedele.Tests.Shared;

namespace Fedele.Core.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void FedeleCore_References_NoOtherFedeleAssembly()
    {
        Assert.Empty(AssemblyReferences.FedeleReferencesOf("Fedele.Core"));
    }

    [Fact]
    public void FedeleCore_References_OnlyFrameworkAssemblies()
    {
        Assert.Empty(AssemblyReferences.ExternalReferencesOf("Fedele.Core"));
    }
}
