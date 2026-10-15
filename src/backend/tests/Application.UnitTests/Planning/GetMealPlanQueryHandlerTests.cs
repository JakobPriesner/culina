using Application.Abstractions;
using Application.Planning;
using Contracts.Planning;
using Domain.Households;
using Domain.Planning;
using Domain.Shared;
using TestSupport;

namespace Application.UnitTests.Planning;

/// <summary>Which week a plan is read for, and that a day off the calendar is refused rather than thrown on.</summary>
public class GetMealPlanQueryHandlerTests
{
    private static readonly Guid HouseholdId = Guid.NewGuid();

    private static readonly Guid UserId = Guid.NewGuid();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ShouldReadTheWeekOfTheClock_WhenNoDayIsGiven()
    {
        // Arrange: Thursday 17 September 2026, 23:30 UTC.
        var plans = new RecordingPlans();
        var handler = HandlerAt(plans, new DateTimeOffset(2026, 9, 17, 23, 30, 0, TimeSpan.Zero), member: true);

        // Act
        var result = await handler.Handle(new GetMealPlanQuery(HouseholdId, UserId, null), Token);

        // Assert
        var week = result.ShouldBeSuccess();
        Assert.Equal(new DateOnly(2026, 9, 14), week.From);
        Assert.Equal(new DateOnly(2026, 9, 14), plans.AskedFrom);
    }

    [Fact]
    public async Task Handle_ShouldSnapAGivenDayToItsMonday()
    {
        // Arrange
        var plans = new RecordingPlans();
        var handler = HandlerAt(plans, DateTimeOffset.UnixEpoch, member: true);

        // Act
        var result = await handler.Handle(new GetMealPlanQuery(HouseholdId, UserId, new DateOnly(2026, 9, 20)), Token);

        // Assert
        Assert.Equal(new DateOnly(2026, 9, 14), result.ShouldBeSuccess().From);
    }

    [Theory]
    [InlineData("0001-01-03")]
    [InlineData("9999-12-28")]
    [InlineData("9999-12-31")]
    public async Task Handle_ShouldReturnAValidationError_WhenTheDayIsTooNearTheEdgeOfTheCalendar(string day)
    {
        // Arrange
        var plans = new RecordingPlans();
        var handler = HandlerAt(plans, DateTimeOffset.UnixEpoch, member: true);

        // Act
        var result = await handler.Handle(
            new GetMealPlanQuery(HouseholdId, UserId, DateOnly.Parse(day, System.Globalization.CultureInfo.InvariantCulture)),
            Token);

        // Assert
        result.ShouldBeFailure(PlanningErrors.InvalidDate);
        Assert.Null(plans.AskedFrom);
    }

    [Fact]
    public async Task Handle_ShouldNotFindTheHousehold_WhenTheCallerIsNotInIt()
    {
        // Arrange
        var handler = HandlerAt(new RecordingPlans(), DateTimeOffset.UnixEpoch, member: false);

        // Act
        var result = await handler.Handle(new GetMealPlanQuery(HouseholdId, UserId, null), Token);

        // Assert
        result.ShouldBeFailure(HouseholdErrors.NotFound(HouseholdId));
    }

    private static GetMealPlanQueryHandler HandlerAt(RecordingPlans plans, DateTimeOffset now, bool member) =>
        new(plans, new Members(member), new FixedClock(now));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingPlans : IMealPlanRepository
    {
        public DateOnly? AskedFrom { get; private set; }

        public Task<IReadOnlyList<PlannedRecipe>> ForWeekAsync(
            Guid householdId, DateOnly from, int days, CancellationToken cancellationToken)
        {
            AskedFrom = from;

            return Task.FromResult<IReadOnlyList<PlannedRecipe>>([]);
        }

        public Task<int> NextSortOrderAsync(Guid householdId, DateOnly date, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> AddAsync(MealPlanEntry entry, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<MealPlanEntry>> FindAsync(Guid entryId, Guid householdId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> MoveAsync(MealPlanEntry moved, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<DateOnly>> RemoveAsync(Guid entryId, Guid householdId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class Members(bool isMember) : IHouseholdRepository
    {
        public Task<bool> IsMemberAsync(Guid householdId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(isMember);

        public Task<Result<Household>> FindAsync(Guid householdId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Household>> ForUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> AddAsync(Household household, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<long>> UpdateAsync(Household household, long expectedVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> CanSeeRecipesAsync(Guid householdId, Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<InheritedHousehold>> AncestorsAsync(Guid householdId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> LibraryOfMemberAsync(Guid householdId, Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Heir>> HeirsAsync(Guid householdId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<HouseholdMemberView>> MembersAsync(Guid householdId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> DeleteAsync(
            Guid householdId, long expectedVersion, Guid deletedBy, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
