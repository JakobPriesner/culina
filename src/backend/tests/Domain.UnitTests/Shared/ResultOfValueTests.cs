using Domain.Shared;

namespace Domain.UnitTests.Shared;

public class ResultOfValueTests
{
    private static readonly Error AnyError = new("tests.any", "Anything.", ErrorType.NotFound);

    [Fact]
    public void Match_ShouldExposeTheValue_WhenTheResultIsSuccessful()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var observed = result.Match(value => value, _ => -1);

        // Assert
        Assert.Equal(42, observed);
    }

    [Fact]
    public void Match_ShouldExposeTheError_WhenTheResultFailed()
    {
        // Arrange
        var result = Result<int>.Failure(AnyError);

        // Act
        var code = result.Match(_ => string.Empty, error => error.Code);

        // Assert
        Assert.Equal(AnyError.Code, code);
    }

    [Fact]
    public void ImplicitConversion_ShouldProduceASuccess_WhenAValueIsReturned()
    {
        // Arrange
        Result<string> result = "ready";

        // Act
        var observed = result.Match(value => value, error => error.Code);

        // Assert
        Assert.Equal("ready", observed);
    }

    [Fact]
    public void ImplicitConversion_ShouldProduceAFailure_WhenAnErrorIsReturned()
    {
        // Arrange
        Result<string> result = AnyError;

        // Act
        var observed = result.Match(value => value, error => error.Code);

        // Assert
        Assert.Equal(AnyError.Code, observed);
    }

    [Fact]
    public void Success_ShouldThrow_WhenTheValueIsNull()
    {
        // Arrange
        string? missing = null;

        // Act
        void Act() => Result<string>.Success(missing!);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    [Fact]
    public void Match_ShouldThrow_WhenTheResultWasNeverAssignedAnOutcome()
    {
        // Arrange
        var uninitialised = default(Result<int>);

        // Act
        void Act() => uninitialised.Match(value => value, _ => -1);

        // Assert
        Assert.Throws<InvalidOperationException>(Act);
    }
}
