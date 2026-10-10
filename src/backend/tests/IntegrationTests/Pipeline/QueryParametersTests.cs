using Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using TestSupport;

namespace IntegrationTests.Pipeline;

/// <summary>The query-string reader: absent, valid, and every way of being wrong.</summary>
public class QueryParametersTests
{
    private static QueryCollection Query(params (string Name, string Value)[] pairs) =>
        new QueryCollection(pairs.ToDictionary(pair => pair.Name, pair => new StringValues(pair.Value), StringComparer.Ordinal));

    [Fact]
    public void RequireGuid_ShouldBeMissing_WhenAbsent() =>
        Query().RequireGuid("householdId").ShouldBeFailure(RequestErrors.MissingQueryParameter("householdId"));

    [Fact]
    public void RequireGuid_ShouldBeInvalid_WhenNotAnId() =>
        Query(("householdId", "abc")).RequireGuid("householdId")
            .ShouldBeFailure(RequestErrors.InvalidQueryParameter("householdId", "an id"));

    [Fact]
    public void RequireGuid_ShouldReadAnId()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, Query(("householdId", id.ToString())).RequireGuid("householdId").ShouldBeSuccess());
    }

    [Fact]
    public void ReadGuid_ShouldBeNothing_WhenAbsent() =>
        Assert.Null(Query().ReadGuid("cookbookId").ShouldBeSuccess().Value);

    [Fact]
    public void ReadGuid_ShouldBeInvalid_WhenEmpty() =>
        Query(("cookbookId", "")).ReadGuid("cookbookId")
            .ShouldBeFailure(RequestErrors.InvalidQueryParameter("cookbookId", "an id"));

    [Fact]
    public void ReadGuids_ShouldFailOnTheFirstBadOne()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["exclude"] = new StringValues([Guid.NewGuid().ToString(), "nope"])
        });

        query.ReadGuids("exclude").ShouldBeFailure(RequestErrors.InvalidQueryParameter("exclude", "an id"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("1.5")]
    [InlineData("1e1")]
    [InlineData(" 5")]
    [InlineData("")]
    public void ReadInt_ShouldBeInvalid_WhenNotAWholeNumberWithinBounds(string value) =>
        Query(("limit", value)).ReadInt("limit", 1, 100, 24)
            .ShouldBeFailure(RequestErrors.InvalidQueryParameter("limit", "a whole number from 1 to 100"));

    [Theory]
    [InlineData("1", 1)]
    [InlineData("100", 100)]
    [InlineData("+7", 7)]
    public void ReadInt_ShouldRead_WhenWithinBounds(string value, int expected) =>
        Assert.Equal(expected, Query(("limit", value)).ReadInt("limit", 1, 100, 24).ShouldBeSuccess());

    [Fact]
    public void ReadInt_ShouldUseTheFallback_WhenAbsent() =>
        Assert.Equal(24, Query().ReadInt("limit", 1, 100, 24).ShouldBeSuccess());

    [Fact]
    public void ReadInt_ShouldHaveNoCeiling_WhenNoneIsGiven() =>
        Assert.Equal(5000, Query(("maxMinutes", "5000")).ReadInt("maxMinutes", 1).ShouldBeSuccess().Value);

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    public void ReadBool_ShouldReadEitherCase(string value, bool expected) =>
        Assert.Equal(expected, Query(("completed", value)).ReadBool("completed").ShouldBeSuccess());

    [Theory]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("")]
    public void ReadBool_ShouldBeInvalid_WhenNotTrueOrFalse(string value) =>
        Query(("completed", value)).ReadBool("completed")
            .ShouldBeFailure(RequestErrors.InvalidQueryParameter("completed", "'true' or 'false'"));

    [Fact]
    public void ReadBool_ShouldBeFalse_WhenAbsent() =>
        Assert.False(Query().ReadBool("completed").ShouldBeSuccess());

    [Fact]
    public void ReadDate_ShouldReadAnIsoDay() =>
        Assert.Equal(new DateOnly(2026, 10, 1), Query(("from", "2026-10-01")).ReadDate("from").ShouldBeSuccess().Value);

    [Theory]
    [InlineData("01.10.2026")]
    [InlineData("10/01/2026")]
    [InlineData("2026-10-1")]
    [InlineData("2026-02-30")]
    public void ReadDate_ShouldBeInvalid_WhenNotIsoFormatted(string value) =>
        Query(("from", value)).ReadDate("from")
            .ShouldBeFailure(RequestErrors.InvalidQueryParameter("from", "a day written yyyy-MM-dd"));
}
