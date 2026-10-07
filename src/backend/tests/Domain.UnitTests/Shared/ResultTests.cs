using Domain.Shared;

namespace Domain.UnitTests.Shared;

public class ResultTests
{
    private static readonly Error AnyError = new("tests.any", "Anything.", ErrorType.Failure);

    [Fact]
    public void Match_ShouldRunTheSuccessBranch_WhenTheResultIsSuccessful()
    {
        var result = Result.Success();

        var branch = result.Match(() => "success", _ => "failure");

        Assert.Equal("success", branch);
    }

    [Fact]
    public void Match_ShouldRunTheFailureBranchWithTheError_WhenTheResultFailed()
    {
        var result = Result.Failure(AnyError);

        var code = result.Match(() => "success", error => error.Code);

        Assert.Equal(AnyError.Code, code);
    }

    [Fact]
    public void ImplicitConversion_ShouldProduceAFailure_WhenAnErrorIsReturned()
    {
        Result result = AnyError;

        var code = result.Match(() => string.Empty, error => error.Code);

        Assert.Equal(AnyError.Code, code);
    }

    [Fact]
    public void Failure_ShouldThrow_WhenTheErrorIsNull()
    {
        Error? missing = null;

        void Act() => Result.Failure(missing!);

        Assert.Throws<ArgumentNullException>(Act);
    }

    [Fact]
    public void Match_ShouldThrow_WhenTheResultWasNeverAssignedAnOutcome()
    {
        var uninitialised = default(Result);

        void Act() => uninitialised.Match(() => 0, _ => 1);

        Assert.Throws<InvalidOperationException>(Act);
    }

    [Fact]
    public void VoidMatch_ShouldRunExactlyOneBranch_WhenObserved()
    {
        var result = Result.Failure(AnyError);
        var ran = new List<string>();

        result.Match(() => ran.Add("success"), _ => ran.Add("failure"));

        Assert.Equal(["failure"], ran);
    }
}
