using System.Reflection;

namespace ArchitectureTests;

/// <summary>
/// Bootstrap settings must fail the process at startup rather than fail the
/// first request that needed them, which only works if every record actually
/// has the check.
/// </summary>
public class SettingsConventionTests
{
    [Fact]
    public void EverySettingsRecord_ShouldHaveAValidateMethod_SoStartupCanRefuseBadConfiguration()
    {
        // Arrange
        var records = SettingsRecords().ToList();

        // Act
        var offenders = records
            .Where(type => type.GetMethod("Validate", Type.EmptyTypes) is null)
            .Select(type => type.FullName!);

        // Assert
        Assert.Empty(offenders);
    }

    [Fact]
    public void EverySettingsRecord_ShouldDeclareItsSectionOrGroupName_SoItCanBeFound()
    {
        // Arrange
        var records = SettingsRecords().ToList();

        // Act
        var offenders = records
            .Where(type => type.GetField("SectionName") is null && type.GetField("GroupName") is null)
            .Select(type => type.FullName!);

        // Assert
        Assert.Empty(offenders);
    }

    [Fact]
    public void SettingsRecords_ShouldExist_SoTheRulesAreActuallyBeingChecked()
    {
        // Arrange & Act
        var records = SettingsRecords().ToList();

        // Assert
        Assert.NotEmpty(records);
    }

    private static IEnumerable<Type> SettingsRecords() =>
        CulinaAssemblies.TypesIn(CulinaAssemblies.Application)
            .Where(type => type.Namespace == "Application.Abstractions.Settings")
            .Where(type => type.IsClass && type.IsSealed)
            .Where(type => type.Name.EndsWith("Settings", StringComparison.Ordinal));
}
