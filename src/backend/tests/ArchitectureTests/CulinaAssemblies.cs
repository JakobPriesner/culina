using System.Reflection;

namespace ArchitectureTests;

/// <summary>The assemblies whose relationships the architecture rules govern.</summary>
internal static class CulinaAssemblies
{
    internal static Assembly Domain => typeof(Domain.AssemblyMarker).Assembly;

    internal static Assembly Contracts => typeof(Contracts.AssemblyMarker).Assembly;

    internal static Assembly Application => typeof(Application.AssemblyMarker).Assembly;

    internal static Assembly Infrastructure => typeof(Infrastructure.AssemblyMarker).Assembly;

    internal static Assembly Api => typeof(Program).Assembly;

    internal static IReadOnlyList<Assembly> All => [Domain, Contracts, Application, Infrastructure, Api];

    private static readonly HashSet<string> Names =
        ["Domain", "Contracts", "Application", "Infrastructure", "Api"];

    internal static bool Contains(string assemblyName) => Names.Contains(assemblyName);

    /// <summary>Every type we wrote, excluding compiler-generated ones.</summary>
    internal static IEnumerable<Type> TypesIn(Assembly assembly) =>
        assembly.GetTypes().Where(type => !type.IsCompilerGenerated());

    private static bool IsCompilerGenerated(this Type type) =>
        type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false)
        || type.Name.Contains('<', StringComparison.Ordinal);
}
