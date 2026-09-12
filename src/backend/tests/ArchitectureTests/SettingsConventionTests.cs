using System.Reflection;
using Application.Abstractions.Settings;

namespace ArchitectureTests;

/// <summary>
/// Settings come in two shapes, and the difference is not cosmetic.
/// Bootstrap settings are read from the environment before the database can be
/// reached, are immutable, and must fail the process at startup if they are
/// unusable. Instance settings are rows an admin edits while the app runs, so
/// they are mutable singletons and there is nothing to validate at startup.
/// </summary>
public class SettingsConventionTests
{
    [Fact]
    public void EverySettingsRecord_ShouldBeEitherBootstrapOrInstance_AndNotBoth()
    {
        // Arrange
        var records = SettingsRecords().ToList();

        // Act
        var offenders = records
            .Where(type => IsBootstrap(type) == IsInstance(type))
            .Select(type => type.FullName!);

        // Assert
        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryBootstrapRecord_ShouldHaveAValidateMethod_SoStartupCanRefuseBadConfiguration()
    {
        // Arrange
        var records = SettingsRecords().Where(IsBootstrap).ToList();

        // Act
        var offenders = records
            .Where(type => type.GetMethod("Validate", Type.EmptyTypes) is null)
            .Select(type => type.FullName!);

        // Assert
        // A misconfigured process must fail to start loudly rather than fail on
        // the first request that needed the value.
        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryBootstrapRecord_ShouldBeImmutable_BecauseOnlyARestartChangesIt()
    {
        // Arrange
        var records = SettingsRecords().Where(IsBootstrap).ToList();

        // Act
        var offenders = records
            .SelectMany(type => type.GetProperties().Select(property => (type, property)))
            .Where(pair => pair.property.SetMethod is { } setter && !IsInitOnly(setter))
            .Select(pair => $"{pair.type.Name}.{pair.property.Name}");

        // Assert
        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryInstanceRecord_ShouldBeMutable_BecauseAnUpdateChangesItInPlace()
    {
        // Arrange
        var records = SettingsRecords().Where(IsInstance).ToList();

        // Act
        var offenders = records
            .Where(type => !type.GetProperties().Any(property =>
                property.SetMethod is { } setter && !IsInitOnly(setter)))
            .Select(type => type.FullName!);

        // Assert
        // Live updates work by mutating the one shared singleton every consumer
        // already holds; an init-only group could never be updated.
        Assert.Empty(offenders);
    }

    [Fact]
    public void SettingsRecordsOfBothKinds_ShouldExist_SoTheRulesAreActuallyBeingChecked()
    {
        // Arrange & Act
        var records = SettingsRecords().ToList();

        // Assert
        Assert.Contains(records, IsBootstrap);
        Assert.Contains(records, IsInstance);
    }

    private static bool IsBootstrap(Type type) => type.GetField("SectionName") is not null;

    private static bool IsInstance(Type type) =>
        type.GetInterfaces().Any(contract =>
            contract.IsGenericType
            && contract.GetGenericTypeDefinition() == typeof(IInstanceSettings<>));

    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers()
            .Any(modifier => modifier.Name == "IsExternalInit");

    private static IEnumerable<Type> SettingsRecords() =>
        CulinaAssemblies.TypesIn(CulinaAssemblies.Application)
            .Where(type => type.Namespace == "Application.Abstractions.Settings")
            .Where(type => type.IsClass && type.IsSealed)
            .Where(type => type.Name.EndsWith("Settings", StringComparison.Ordinal));
}
