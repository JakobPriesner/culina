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
        // Contracts is a leaf on purpose: an Application handler returns the
        // API-shaped response directly, which only works if both can see the
        // DTOs without either depending on the other.
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
        // A subset rather than an exact match: the compiler elides a reference
        // no type actually uses, so absence is not a violation and asserting it
        // would make the rule fail for the wrong reason.
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
