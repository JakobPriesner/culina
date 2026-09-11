using Domain.Shared;

namespace Domain.UnitTests.Shared;

public class ResultTests
{
    private static readonly Error AnyError = new("tests.any", "Anything.", ErrorType.Failure);

    [Fact]
    public void Match_ShouldRunTheSuccessBranch_WhenTheResultIsSuccessful()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var branch = result.Match(() => "success", _ => "failure");

        // Assert
        Assert.Equal("success", branch);
    }

    [Fact]
    public void Match_ShouldRunTheFailureBranchWithTheError_WhenTheResultFailed()
    {
        // Arrange
        var result = Result.Failure(AnyError);

        // Act
        var code = result.Match(() => "success", error => error.Code);

        // Assert
        Assert.Equal(AnyError.Code, code);
    }

    [Fact]
    public void ImplicitConversion_ShouldProduceAFailure_WhenAnErrorIsReturned()
    {
        // Arrange
        Result result = AnyError;

        // Act
        var code = result.Match(() => string.Empty, error => error.Code);

        // Assert
        Assert.Equal(AnyError.Code, code);
    }

    [Fact]
    public void Failure_ShouldThrow_WhenTheErrorIsNull()
    {
        // Arrange
        Error? missing = null;

        // Act
        void Act() => Result.Failure(missing!);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    [Fact]
    public void Match_ShouldThrow_WhenTheResultWasNeverAssignedAnOutcome()
    {
        // Arrange
        var uninitialised = default(Result);

        // Act
        void Act() => uninitialised.Match(() => 0, _ => 1);

        // Assert
        Assert.Throws<InvalidOperationException>(Act);
    }

    [Fact]
    public void VoidMatch_ShouldRunExactlyOneBranch_WhenObserved()
    {
        // Arrange
        var result = Result.Failure(AnyError);
        var ran = new List<string>();

        // Act
        result.Match(() => ran.Add("success"), _ => ran.Add("failure"));

        // Assert
        Assert.Equal(["failure"], ran);
    }
}
