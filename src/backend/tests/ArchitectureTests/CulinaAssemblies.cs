namespace ArchitectureTests;

/// <summary>The assemblies whose relationships the architecture rules govern.</summary>
internal static class CulinaAssemblies
{
    private static readonly HashSet<string> Names =
        ["Domain", "Contracts", "Application", "Infrastructure", "Api"];

    internal static bool Contains(string assemblyName) => Names.Contains(assemblyName);
}
