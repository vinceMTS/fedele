using System.Reflection;

namespace Fedele.Tests.Shared;

/// <summary>
/// Helpers for architecture tests that enforce the dependency rules in docs/architettura.md.
/// </summary>
internal static class AssemblyReferences
{
    /// <summary>
    /// Returns the names of the Fedele assemblies referenced by the given assembly.
    /// </summary>
    public static IReadOnlyList<string> FedeleReferencesOf(string assemblyName) =>
        Assembly.Load(new AssemblyName(assemblyName))
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(IsFedele)
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Returns the names of the referenced assemblies that are neither part of .NET nor of Fedele.
    /// </summary>
    public static IReadOnlyList<string> ExternalReferencesOf(string assemblyName) =>
        Assembly.Load(new AssemblyName(assemblyName))
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => !IsFramework(name) && !IsFedele(name))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static bool IsFedele(string name) =>
        name.StartsWith("Fedele.", StringComparison.Ordinal) || name == "fedele";

    // An assembly belongs to .NET when it ships in the shared runtime directory.
    private static readonly string _runtimeDirectory =
        Path.GetDirectoryName(typeof(object).Assembly.Location)!;

    private static bool IsFramework(string name) =>
        File.Exists(Path.Combine(_runtimeDirectory, name + ".dll"));
}
