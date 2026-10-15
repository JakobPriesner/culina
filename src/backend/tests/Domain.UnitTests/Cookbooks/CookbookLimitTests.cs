using Domain.Cookbooks;
using TestSupport;

namespace Domain.UnitTests.Cookbooks;

public class CookbookLimitTests
{
    [Fact]
    public void EnsureRoom_ShouldAccept_UntilTheHouseholdHasItsLimit()
    {
        Cookbook.EnsureRoom(Cookbook.MaxPerHousehold - 1).ShouldBeSuccess();
    }

    [Fact]
    public void EnsureRoom_ShouldRefuse_WhenTheHouseholdIsAtItsLimit()
    {
        Cookbook.EnsureRoom(Cookbook.MaxPerHousehold).ShouldBeFailure(CookbookErrors.TooMany);
    }
}
