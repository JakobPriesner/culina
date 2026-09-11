using Domain.Shared;

namespace Domain.UnitTests.Shared;

public class ResultAsyncTests
{
    private static readonly Error Failed = new("tests.failed", "Failed.", ErrorType.Failure);

    [Fact]
    public async Task MapAsync_ShouldTransformTheValue_WhenThePendingResultSucceeds()
    {
        // Arrange
        var pending = Task.FromResult(Result<int>.Success(21));

        // Act
        var mapped = await pending.MapAsync(value => value * 2);

        // Assert
        Assert.Equal(42, mapped.Match(value => value, _ => -1));
    }

    [Fact]
    public async Task BindAsync_ShouldNotRunTheContinuation_WhenThePendingResultFailed()
    {
        // Arrange
        var pending = Task.FromResult(Result<int>.Failure(Failed));
        var ran = false;

        // Act
        var chained = await pending.BindAsync(value =>
        {
            ran = true;
            return Task.FromResult(Result<int>.Success(value));
        });

        // Assert
        Assert.False(ran);
        Assert.Equal(Failed.Code, chained.Match(_ => string.Empty, error => error.Code));
    }

    [Fact]
    public async Task BindAsync_ShouldChainTheContinuation_WhenThePendingResultSucceeds()
    {
        // Arrange
        var pending = Task.FromResult(Result<int>.Success(2));

        // Act
        var chained = await pending.BindAsync(value => Task.FromResult(Result<string>.Success($"n={value}")));

        // Assert
        Assert.Equal("n=2", chained.Match(value => value, error => error.Code));
    }

    [Fact]
    public async Task EnsureAsync_ShouldFail_WhenThePredicateRejectsTheValue()
    {
        // Arrange
        var pending = Task.FromResult(Result<int>.Success(1));

        // Act
        var guarded = await pending.EnsureAsync(value => value > 10, Failed);

        // Assert
        Assert.Equal(Failed.Code, guarded.Match(_ => string.Empty, error => error.Code));
    }
}
