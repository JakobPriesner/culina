using Domain.Shared;

namespace Domain.UnitTests.Shared;

public class ResultCompositionTests
{
    private static readonly Error First = new("tests.first", "First.", ErrorType.Validation);

    [Fact]
    public void Map_ShouldTransformTheValue_WhenTheResultIsSuccessful()
    {
        var result = Result<int>.Success(21);

        var doubled = result.Map(value => value * 2);

        Assert.Equal(42, doubled.Match(value => value, _ => -1));
    }

    [Fact]
    public void Map_ShouldNotRunTheProjection_WhenTheResultFailed()
    {
        var result = Result<int>.Failure(First);
        var ran = false;

        var mapped = result.Map(value =>
        {
            ran = true;
            return value;
        });

        Assert.False(ran);
        Assert.Equal(First.Code, mapped.Match(_ => string.Empty, error => error.Code));
    }

    [Fact]
    public void Bind_ShouldShortCircuit_WhenAnEarlierStepFailed()
    {
        var result = Result<int>.Failure(First);

        var chained = result
            .Bind(value => Result<int>.Success(value + 1))
            .Bind(value => Result<string>.Success(value.ToString(null as IFormatProvider)));

        Assert.Equal(First.Code, chained.Match(_ => string.Empty, error => error.Code));
    }

    [Fact]
    public void Tap_ShouldRunTheSideEffectAndKeepTheValue_WhenSuccessful()
    {
        var seen = 0;

        var tapped = Result<int>.Success(7).Tap(value => seen = value);

        Assert.Equal(7, seen);
        Assert.Equal(7, tapped.Match(value => value, _ => -1));
    }
}
