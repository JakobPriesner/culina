using Domain.Searches;
using TestSupport;

namespace Domain.UnitTests.Searches;

public class SavedSearchLimitTests
{
    [Fact]
    public void EnsureRoom_ShouldAccept_UntilTheHouseholdHasItsLimit()
    {
        SavedSearch.EnsureRoom(SavedSearch.MaxPerHousehold - 1).ShouldBeSuccess();
    }

    [Fact]
    public void EnsureRoom_ShouldRefuse_WhenTheHouseholdIsAtItsLimit()
    {
        SavedSearch.EnsureRoom(SavedSearch.MaxPerHousehold).ShouldBeFailure(SavedSearchErrors.TooMany);
    }
}
