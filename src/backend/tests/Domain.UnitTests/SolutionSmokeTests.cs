namespace Domain.UnitTests;

/// <summary>
/// Proves the test harness itself runs. Deleted as soon as Domain has a real
/// behaviour to assert.
/// </summary>
public class SolutionSmokeTests
{
    [Fact]
    public void TestHost_ShouldRun_WhenTheProjectIsConfigured()
    {
        // Arrange
        const int expected = 1;

        // Act
        var actual = expected;

        // Assert
        Assert.Equal(expected, actual);
    }
}
