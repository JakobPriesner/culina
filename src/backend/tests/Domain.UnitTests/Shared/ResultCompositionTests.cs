using Domain.Shared;

namespace Domain.UnitTests.Shared;

public class ResultCompositionTests
{
    private static readonly Error First = new("tests.first", "First.", ErrorType.Validation);

    [Fact]
    public void Map_ShouldTransformTheValue_WhenTheResultIsSuccessful()
    {
        // Arrange
        var result = Result<int>.Success(21);

        // Act
        var doubled = result.Map(value => value * 2);

        // Assert
        Assert.Equal(42, doubled.Match(value => value, _ => -1));
    }

    [Fact]
    public void Map_ShouldNotRunTheProjection_WhenTheResultFailed()
    {
        // Arrange
        var result = Result<int>.Failure(First);
        var ran = false;

        // Act
        var mapped = result.Map(value =>
        {
            ran = true;
            return value;
        });

        // Assert
        Assert.False(ran);
        Assert.Equal(First.Code, mapped.Match(_ => string.Empty, error => error.Code));
    }

    [Fact]
    public void Bind_ShouldShortCircuit_WhenAnEarlierStepFailed()
    {
        // Arrange
        var result = Result<int>.Failure(First);

        // Act
        var chained = result
            .Bind(value => Result<int>.Success(value + 1))
            .Bind(value => Result<string>.Success(value.ToString(null as IFormatProvider)));

        // Assert
        Assert.Equal(First.Code, chained.Match(_ => string.Empty, error => error.Code));
    }

    [Fact]
    public void Tap_ShouldRunTheSideEffectAndKeepTheValue_WhenSuccessful()
    {
        // Arrange
        var seen = 0;

        // Act
        var tapped = Result<int>.Success(7).Tap(value => seen = value);

        // Assert
        Assert.Equal(7, seen);
        Assert.Equal(7, tapped.Match(value => value, _ => -1));
    }
}
