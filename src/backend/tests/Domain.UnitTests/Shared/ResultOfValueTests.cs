using Domain.Shared;

namespace Domain.UnitTests.Shared;

public class ResultOfValueTests
{
    private static readonly Error AnyError = new("tests.any", "Anything.", ErrorType.NotFound);

    [Fact]
    public void Match_ShouldExposeTheValue_WhenTheResultIsSuccessful()
    {
        var result = Result<int>.Success(42);

        var observed = result.Match(value => value, _ => -1);

        Assert.Equal(42, observed);
    }

    [Fact]
    public void Match_ShouldExposeTheError_WhenTheResultFailed()
    {
        var result = Result<int>.Failure(AnyError);

        var code = result.Match(_ => string.Empty, error => error.Code);

        Assert.Equal(AnyError.Code, code);
    }

    [Fact]
    public void ImplicitConversion_ShouldProduceASuccess_WhenAValueIsReturned()
    {
        Result<string> result = "ready";

        var observed = result.Match(value => value, error => error.Code);

        Assert.Equal("ready", observed);
    }

    [Fact]
    public void ImplicitConversion_ShouldProduceAFailure_WhenAnErrorIsReturned()
    {
        Result<string> result = AnyError;

        var observed = result.Match(value => value, error => error.Code);

        Assert.Equal(AnyError.Code, observed);
    }

    [Fact]
    public void Success_ShouldThrow_WhenTheValueIsNull()
    {
        string? missing = null;

        void Act() => Result<string>.Success(missing!);

        Assert.Throws<ArgumentNullException>(Act);
    }

    [Fact]
    public void Match_ShouldThrow_WhenTheResultWasNeverAssignedAnOutcome()
    {
        var uninitialised = default(Result<int>);

        void Act() => uninitialised.Match(value => value, _ => -1);

        Assert.Throws<InvalidOperationException>(Act);
    }
}
