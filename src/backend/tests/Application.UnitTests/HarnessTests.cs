namespace Application.UnitTests;

/// <summary>
/// Placeholder proving the harness runs. Replaced by real handler tests as
/// soon as Application has a handler (see the phase 3 beads).
/// </summary>
public class HarnessTests
{
    [Fact]
    public void ApplicationAssembly_ShouldBeLoadable_WhenTheProjectIsReferenced()
    {
        // Arrange
        var marker = typeof(Application.AssemblyMarker);

        // Act
        var assemblyName = marker.Assembly.GetName().Name;

        // Assert
        Assert.Equal("Application", assemblyName);
    }
}
