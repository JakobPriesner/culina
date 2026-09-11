using Domain.Shared;

namespace Domain.UnitTests.Shared;

public class ResultCompositionTests
{
    private static readonly Error First = new("tests.first", "First.", ErrorType.Validation);
    private static readonly Error NotFound = new("tests.absent", "Absent.", ErrorType.NotFound);

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
    public void Ensure_ShouldFail_WhenThePredicateRejectsTheValue()
    {
        // Arrange
        var result = Result<int>.Success(3);

        // Act
        var guarded = result.Ensure(value => value > 10, First);

        // Assert
        Assert.Equal(First.Code, guarded.Match(_ => string.Empty, error => error.Code));
    }

    [Fact]
    public void Ensure_ShouldPassTheValueThrough_WhenThePredicateAcceptsIt()
    {
        // Arrange
        var result = Result<int>.Success(30);

        // Act
        var guarded = result.Ensure(value => value > 10, First);

        // Assert
        Assert.Equal(30, guarded.Match(value => value, _ => -1));
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

    [Fact]
    public void ToResult_ShouldFailWithTheGivenError_WhenTheReferenceIsAbsent()
    {
        // Arrange
        string? absent = null;

        // Act
        var result = absent.ToResult(NotFound);

        // Assert
        Assert.Equal(NotFound.Code, result.Match(value => value, error => error.Code));
    }

    [Fact]
    public void ToResult_ShouldSucceed_WhenTheValueTypeIsPresent()
    {
        // Arrange
        int? present = 5;

        // Act
        var result = present.ToResult(NotFound);

        // Assert
        Assert.Equal(5, result.Match(value => value, _ => -1));
    }
}
