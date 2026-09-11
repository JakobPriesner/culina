using System.Diagnostics;
using Application.Telemetry;
using Domain.Shared;

namespace Application.UnitTests.Telemetry;

public class UseCaseActivityTests
{
    private static readonly Error Rejected =
        new("recipes.invalid_title", "A title is required.", ErrorType.Validation);

    [Fact]
    public void Record_ShouldMarkTheSpanFailedWithTheErrorCode_WhenTheHandlerFails()
    {
        // Arrange
        using var listener = ListenToCulinaSpans(out var finished);

        // Act
        using (var tracked = UseCaseActivity.Start("Recipes.Create"))
        {
            tracked.Record(Result<int>.Failure(Rejected));
        }

        // Assert
        var span = Assert.Single(finished);
        Assert.Equal("Recipes.Create", span.DisplayName);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        // The code, never the description: descriptions get reworded and would
        // break an alert keyed on the status text.
        Assert.Equal(Rejected.Code, span.StatusDescription);
    }

    [Fact]
    public void Record_ShouldLeaveTheSpanUnset_WhenTheHandlerSucceeds()
    {
        // Arrange
        using var listener = ListenToCulinaSpans(out var finished);

        // Act
        using (var tracked = UseCaseActivity.Start("Recipes.GetById"))
        {
            tracked.Record(Result<int>.Success(1));
        }

        // Assert
        var span = Assert.Single(finished);
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
    }

    [Fact]
    public void Record_ShouldReturnTheResultUnchanged_SoItCanBeUsedInline()
    {
        // Arrange
        using var listener = ListenToCulinaSpans(out _);
        var result = Result<string>.Success("ready");

        // Act
        using var tracked = UseCaseActivity.Start("Recipes.GetById");
        var returned = tracked.Record(result);

        // Assert
        Assert.Equal("ready", returned.Match(value => value, error => error.Code));
    }

    [Fact]
    public void Tag_ShouldAttachTheValueToTheSpan_WhenTheHandlerAddsContext()
    {
        // Arrange
        using var listener = ListenToCulinaSpans(out var finished);
        var recipeId = Guid.CreateVersion7();

        // Act
        using (var tracked = UseCaseActivity.Start("Recipes.Create"))
        {
            tracked.Tag("culina.recipe_id", recipeId);
            tracked.Record(Result.Success());
        }

        // Assert
        var span = Assert.Single(finished);
        Assert.Equal(recipeId, span.GetTagItem("culina.recipe_id"));
    }

    private static ActivityListener ListenToCulinaSpans(out List<Activity> finished)
    {
        var collected = new List<Activity>();
        finished = collected;

        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CulinaTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = collected.Add
        };

        ActivitySource.AddActivityListener(listener);

        return listener;
    }
}
