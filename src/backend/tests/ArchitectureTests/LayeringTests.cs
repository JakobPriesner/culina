using System.Reflection;

namespace ArchitectureTests;

/// <summary>The layering table in <c>dotnet-project-setup</c>: the dependency direction never reverses.</summary>
public class LayeringTests
{
    [Fact]
    public void Domain_ShouldReferenceNothing_WhenItIsALeaf()
    {
        // Arrange
        var assembly = CulinaAssemblies.Domain;

        // Act
        var references = CulinaReferencesOf(assembly);

        // Assert
        Assert.Empty(references);
    }

    [Fact]
    public void Contracts_ShouldReferenceNothing_WhenItIsALeaf()
    {
        // Arrange
        // A leaf on purpose: Application and Api both see the DTOs without depending on each other.
        var assembly = CulinaAssemblies.Contracts;

        // Act
        var references = CulinaReferencesOf(assembly);

        // Assert
        Assert.Empty(references);
    }

    [Fact]
    public void Application_ShouldSeeOnlyDomainAndContracts_WhenItOrchestratesUseCases()
    {
        // Arrange
        var assembly = CulinaAssemblies.Application;

        // Act
        var forbidden = CulinaReferencesOf(assembly).Except(["Domain", "Contracts"], StringComparer.Ordinal);

        // Assert
        // A subset, not an exact match: the compiler elides unused references.
        Assert.Empty(forbidden);
    }

    [Fact]
    public void Infrastructure_ShouldSeeOnlyTheLayersBelowIt_WhenItAdaptsTechnology()
    {
        // Arrange
        var assembly = CulinaAssemblies.Infrastructure;

        // Act
        var forbidden = CulinaReferencesOf(assembly)
            .Except(["Application", "Domain", "Contracts"], StringComparer.Ordinal);

        // Assert
        Assert.Empty(forbidden);
    }

    [Fact]
    public void NoLayer_ShouldReferenceApi_BecauseNothingSitsAboveIt()
    {
        // Arrange
        var below = new[]
        {
            CulinaAssemblies.Domain,
            CulinaAssemblies.Contracts,
            CulinaAssemblies.Application,
            CulinaAssemblies.Infrastructure
        };

        // Act
        var offenders = below.Where(assembly => CulinaReferencesOf(assembly).Contains("Api"));

        // Assert
        Assert.Empty(offenders.Select(assembly => assembly.GetName().Name));
    }

    private static string[] CulinaReferencesOf(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(CulinaAssemblies.Contains)];
}
