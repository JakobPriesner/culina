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
        using var listener = ListenFor("Recipes.Create", out var finished);

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
        using var listener = ListenFor("Recipes.GetById", out var finished);

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
        using var listener = ListenFor("Recipes.GetById", out _);
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
        using var listener = ListenFor("Recipes.Tagged", out var finished);
        var recipeId = Guid.CreateVersion7();

        // Act
        using (var tracked = UseCaseActivity.Start("Recipes.Tagged"))
        {
            tracked.Tag("culina.recipe_id", recipeId);
            tracked.Record(Result.Success());
        }

        // Assert
        var span = Assert.Single(finished);
        Assert.Equal(recipeId, span.GetTagItem("culina.recipe_id"));
    }

    /// <summary>
    /// Collects the spans this test starts, and nobody else's.
    /// </summary>
    /// <param name="named">The name the test gives its own span.</param>
    /// <param name="finished">Where the spans land.</param>
    /// <remarks>
    /// <para>
    /// An <see cref="ActivityListener"/> is process-wide: it hears every span
    /// from the Culina source, including ones started by handler tests running
    /// in parallel. Filtering by name is what makes <c>Assert.Single</c> a
    /// statement about this test rather than about how much of the suite
    /// happened to be running beside it.
    /// </para>
    /// <para>
    /// Locked, for the same reason. The callback fires on whichever thread
    /// stopped the span, and a <c>List&lt;T&gt;</c> appended from two of them
    /// does not merely interleave — it corrupts.
    /// </para>
    /// </remarks>
    private static ActivityListener ListenFor(string named, out List<Activity> finished)
    {
        var collected = new List<Activity>();
        var gate = new Lock();

        finished = collected;

        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CulinaTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = span =>
            {
                if (span.DisplayName != named)
                {
                    return;
                }

                lock (gate)
                {
                    collected.Add(span);
                }
            }
        };

        ActivitySource.AddActivityListener(listener);

        return listener;
    }
}
