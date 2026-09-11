using Domain.Shared;
using TestSupport;
using Xunit.Sdk;

namespace Application.UnitTests.TestSupportChecks;

/// <summary>
/// The assertion helpers are used by every other suite, so a mistake in them
/// would quietly weaken hundreds of tests.
/// </summary>
public class ResultAssertionsTests
{
    private static readonly Error Expected = new("users.not_found", "No such user.", ErrorType.NotFound);
    private static readonly Error Other = new("users.email_already_used", "Taken.", ErrorType.Conflict);

    [Fact]
    public void ShouldBeSuccess_ShouldReturnTheValue_WhenTheOperationSucceeded()
    {
        // Arrange
        var result = Result<int>.Success(7);

        // Act
        var value = result.ShouldBeSuccess();

        // Assert
        Assert.Equal(7, value);
    }

    [Fact]
    public void ShouldBeSuccess_ShouldFailWithTheErrorCode_WhenTheOperationFailed()
    {
        // Arrange
        var result = Result<int>.Failure(Expected);

        // Act
        void Act() => result.ShouldBeSuccess();

        // Assert
        var failure = Assert.Throws<XunitException>(Act);
        Assert.Contains("users.not_found", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldBeFailure_ShouldPass_WhenTheCodesMatch()
    {
        // Arrange
        var result = Result<int>.Failure(Expected);

        // Act
        result.ShouldBeFailure(Expected);

        // Assert
        Assert.True(true, "reaching here is the assertion");
    }

    [Fact]
    public void ShouldBeFailure_ShouldFail_WhenADifferentErrorWasReturned()
    {
        // Arrange
        var result = Result<int>.Failure(Other);

        // Act
        void Act() => result.ShouldBeFailure(Expected);

        // Assert
        var failure = Assert.Throws<XunitException>(Act);
        Assert.Contains("users.email_already_used", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldBeFailure_ShouldCompareCodesNotDescriptions_SoRewordingIsSafe()
    {
        // Arrange
        var reworded = Expected with { Description = "That account does not exist." };
        var result = Result<int>.Failure(reworded);

        // Act
        result.ShouldBeFailure(Expected);

        // Assert
        // A description is prose and will be reworded; the code is the contract.
        Assert.True(true, "reaching here is the assertion");
    }
}
