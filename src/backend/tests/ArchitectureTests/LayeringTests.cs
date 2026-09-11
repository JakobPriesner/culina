using System.Reflection;

namespace ArchitectureTests;

/// <summary>
/// The layering table in <c>dotnet-project-setup</c>: the direction never
/// reverses. A ProjectReference that breaks it fails the build, not review.
/// </summary>
public class LayeringTests
{
    [Fact]
    public void Domain_ShouldReferenceNothing_WhenItIsALeaf()
    {
        // Arrange
        var domain = typeof(Domain.AssemblyMarker).Assembly;

        // Act
        var culinaReferences = CulinaReferencesOf(domain);

        // Assert
        Assert.Empty(culinaReferences);
    }

    [Fact]
    public void Contracts_ShouldReferenceNothing_WhenItIsALeaf()
    {
        // Arrange
        var contracts = typeof(Contracts.AssemblyMarker).Assembly;

        // Act
        var culinaReferences = CulinaReferencesOf(contracts);

        // Assert
        Assert.Empty(culinaReferences);
    }

    private static string[] CulinaReferencesOf(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(CulinaAssemblies.Contains)];
}
