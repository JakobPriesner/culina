namespace IntegrationTests;

/// <summary>
/// Placeholder proving the harness runs. Replaced by the Testcontainers-backed
/// API fixture in the phase 2 bead that builds it.
/// </summary>
public class HarnessTests
{
    [Fact]
    public void ApiAssembly_ShouldBeLoadable_WhenTheProjectIsReferenced()
    {
        // Arrange
        var marker = typeof(Program);

        // Act
        var assemblyName = marker.Assembly.GetName().Name;

        // Assert
        Assert.Equal("Api", assemblyName);
    }
}
