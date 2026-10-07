using Domain.Shared;

namespace Domain.UnitTests.Shared;

public class ResultCombineTests
{
    private static readonly FieldError Title =
        new("title", "recipes.invalid_title", "A title is required.");

    private static readonly FieldError Yield =
        new("yield", "recipes.invalid_yield", "The yield must be greater than zero.");

    private static readonly Error Conflict =
        new("recipes.duplicate", "Already exists.", ErrorType.Conflict);

    [Fact]
    public void Combine_ShouldSucceed_WhenEveryCheckPassed()
    {
        var checks = new[] { Result.Success(), Result.Success() };

        var combined = Result.Combine(checks);

        Assert.True(combined.Match(() => true, _ => false));
    }

    [Fact]
    public void Combine_ShouldReportEveryFailure_WhenSeveralChecksFailed()
    {
        var checks = new[] { Result.Failure(Title), Result.Success(), Result.Failure(Yield) };

        var combined = Result.Combine(checks);

        var aggregate = combined.Match(() => null, error => error as ValidationError);
        Assert.NotNull(aggregate);
        Assert.Equal(["title", "yield"], aggregate.Errors.OfType<FieldError>().Select(e => e.Field));
    }

    [Fact]
    public void Combine_ShouldReturnTheFailureUnchanged_WhenExactlyOneCheckFailed()
    {
        var checks = new[] { Result.Success(), Result.Failure(Conflict) };

        var combined = Result.Combine(checks);

        // A lone failure is passed through so combining one check cannot turn a 409 into a 400.
        var type = combined.Match(() => ErrorType.Failure, error => error.Type);
        Assert.Equal(ErrorType.Conflict, type);
    }

    [Fact]
    public void Combine_ShouldFlattenNestedAggregates_WhenCombiningCombinedResults()
    {
        var inner = Result.Combine([Result.Failure(Title), Result.Failure(Yield)]);
        var checks = new[] { inner, Result.Failure(Conflict) };

        var combined = Result.Combine(checks);

        var aggregate = combined.Match(() => null, error => error as ValidationError);
        Assert.NotNull(aggregate);
        Assert.Equal(3, aggregate.Errors.Count);
        Assert.DoesNotContain(aggregate.Errors, error => error is ValidationError);
    }
}
