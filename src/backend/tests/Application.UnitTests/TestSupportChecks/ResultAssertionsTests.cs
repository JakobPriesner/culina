using Domain.Shared;
using TestSupport;
using Xunit.Sdk;

namespace Application.UnitTests.TestSupportChecks;

/// <summary>
/// The assertion helpers back every suite, so a mistake in them would quietly weaken hundreds of
/// tests.
/// </summary>
public class ResultAssertionsTests
{
    private static readonly Error Expected = new("users.not_found", "No such user.", ErrorType.NotFound);
    private static readonly Error Other = new("users.email_already_used", "Taken.", ErrorType.Conflict);

    [Fact]
    public void ShouldBeSuccess_ShouldReturnTheValue_WhenTheOperationSucceeded()
    {
        var result = Result<int>.Success(7);

        var value = result.ShouldBeSuccess();

        Assert.Equal(7, value);
    }

    [Fact]
    public void ShouldBeSuccess_ShouldFailWithTheErrorCode_WhenTheOperationFailed()
    {
        var result = Result<int>.Failure(Expected);

        void Act() => result.ShouldBeSuccess();

        var failure = Assert.Throws<XunitException>(Act);
        Assert.Contains("users.not_found", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldBeFailure_ShouldPass_WhenTheCodesMatch()
    {
        var result = Result<int>.Failure(Expected);

        result.ShouldBeFailure(Expected);

        Assert.True(true, "reaching here is the assertion");
    }

    [Fact]
    public void ShouldBeFailure_ShouldFail_WhenADifferentErrorWasReturned()
    {
        var result = Result<int>.Failure(Other);

        void Act() => result.ShouldBeFailure(Expected);

        var failure = Assert.Throws<XunitException>(Act);
        Assert.Contains("users.email_already_used", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldBeFailure_ShouldCompareCodesNotDescriptions_SoRewordingIsSafe()
    {
        var reworded = Expected with { Description = "That account does not exist." };
        var result = Result<int>.Failure(reworded);

        result.ShouldBeFailure(Expected);

        // A description is prose and will be reworded; the code is the contract.
        Assert.True(true, "reaching here is the assertion");
    }
}
