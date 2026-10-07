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
        using var listener = ListenFor("Recipes.Create", out var finished);

        using (var tracked = UseCaseActivity.Start("Recipes.Create"))
        {
            tracked.Record(Result<int>.Failure(Rejected));
        }

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
        using var listener = ListenFor("Recipes.GetById", out var finished);

        using (var tracked = UseCaseActivity.Start("Recipes.GetById"))
        {
            tracked.Record(Result<int>.Success(1));
        }

        var span = Assert.Single(finished);
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
    }

    [Fact]
    public void Record_ShouldReturnTheResultUnchanged_SoItCanBeUsedInline()
    {
        using var listener = ListenFor("Recipes.GetById", out _);
        var result = Result<string>.Success("ready");

        using var tracked = UseCaseActivity.Start("Recipes.GetById");
        var returned = tracked.Record(result);

        Assert.Equal("ready", returned.Match(value => value, error => error.Code));
    }

    [Fact]
    public void Tag_ShouldAttachTheValueToTheSpan_WhenTheHandlerAddsContext()
    {
        using var listener = ListenFor("Recipes.Tagged", out var finished);
        var recipeId = Guid.CreateVersion7();

        using (var tracked = UseCaseActivity.Start("Recipes.Tagged"))
        {
            tracked.Tag("culina.recipe_id", recipeId);
            tracked.Record(Result.Success());
        }

        var span = Assert.Single(finished);
        Assert.Equal(recipeId, span.GetTagItem("culina.recipe_id"));
    }

    /// <summary>Collects the spans this test starts, and nobody else's.</summary>
    /// <remarks>
    /// An <see cref="ActivityListener"/> is process-wide and hears parallel handler tests too, so
    /// filtering by name keeps <c>Assert.Single</c> about this test. Locked because the callback
    /// fires on whichever thread stopped the span, and an unsynchronised <c>List&lt;T&gt;</c>
    /// corrupts.
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
