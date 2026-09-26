using Domain.Households;
using TestSupport;

namespace Domain.UnitTests.Households;

/// <summary>
/// Membership rules are Domain tests, not HTTP round trips: a rule about who
/// may edit a household is true whether or not the app has an API.
/// </summary>
public class HouseholdMembershipPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly Guid Member = Guid.CreateVersion7();
    private static readonly Guid Stranger = Guid.CreateVersion7();

    [Fact]
    public void CanView_ShouldSayNotFound_WhenTheCallerIsNotAMember()
    {
        // Arrange
        var household = AHousehold();

        // Act
        var result = HouseholdMembershipPolicy.CanView(household, Stranger);

        // Assert
        // Not "forbidden": that would confirm the household exists, which is
        // exactly what a non-member has no business learning.
        result.ShouldBeFailure(HouseholdErrors.NotFound(household.Id));
    }

    [Fact]
    public void CanAdminister_ShouldFail_WhenTheCallerIsOnlyAMember()
    {
        // Arrange
        var household = AHousehold();

        // Act
        var result = HouseholdMembershipPolicy.CanAdminister(household, Member);

        // Assert
        result.ShouldBeFailure(HouseholdErrors.NotOwner);
    }

    [Fact]
    public void CanAdminister_ShouldSucceed_WhenTheCallerIsAnOwner()
    {
        // Arrange
        var household = AHousehold();

        // Act
        var result = HouseholdMembershipPolicy.CanAdminister(household, Owner);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Remove_ShouldSucceed_WhenAMemberLeavesOnTheirOwn()
    {
        // Arrange
        var household = AHousehold();

        // Act
        var result = household.Remove(Member, actingUserId: Member);

        // Assert
        result.ShouldBeSuccess();
        Assert.Null(household.Find(Member));
    }

    [Fact]
    public void Remove_ShouldFail_WhenAMemberTriesToRemoveSomeoneElse()
    {
        // Arrange
        var household = AHousehold();

        // Act
        var result = household.Remove(Owner, actingUserId: Member);

        // Assert
        result.ShouldBeFailure(HouseholdErrors.NotOwner);
    }

    [Fact]
    public void Remove_ShouldFail_WhenItWouldLeaveTheHouseholdWithoutAnOwner()
    {
        // Arrange
        var household = AHousehold();

        // Act
        var result = household.Remove(Owner, actingUserId: Owner);

        // Assert
        // The last owner cannot leave, even voluntarily: a household with no
        // owner can never be administered again.
        result.ShouldBeFailure(HouseholdErrors.LastOwner);
    }

    [Fact]
    public void Remove_ShouldSucceed_WhenAnotherOwnerRemains()
    {
        // Arrange
        var household = AHousehold();
        household.ChangeRole(Member, HouseholdRole.Owner, actingUserId: Owner).ShouldBeSuccess();

        // Act
        var result = household.Remove(Owner, actingUserId: Owner);

        // Assert
        result.ShouldBeSuccess();
        Assert.Equal(1, household.OwnerCount);
    }

    [Fact]
    public void ChangeRole_ShouldFail_WhenDemotingTheOnlyOwner()
    {
        // Arrange
        var household = AHousehold();

        // Act
        var result = household.ChangeRole(Owner, HouseholdRole.Member, actingUserId: Owner);

        // Assert
        result.ShouldBeFailure(HouseholdErrors.LastOwner);
    }

    [Fact]
    public void Remove_ShouldFail_WhenTheTargetIsNotAMember()
    {
        // Arrange
        var household = AHousehold();

        // Act
        var result = household.Remove(Stranger, actingUserId: Owner);

        // Assert
        result.ShouldBeFailure(HouseholdErrors.NotAMember);
    }

    [Fact]
    public void Add_ShouldFail_WhenTheUserIsAlreadyInTheHousehold()
    {
        // Arrange
        var household = AHousehold();

        // Act
        var result = household.Add(Member, HouseholdRole.Member, Now);

        // Assert
        result.ShouldBeFailure(HouseholdErrors.AlreadyAMember);
    }

    [Fact]
    public void Create_ShouldMakeTheCreatorAnOwner_SoTheHouseholdIsAdministrable()
    {
        // Arrange
        var name = HouseholdName.Create("Kitchen").ShouldBeSuccess();

        // Act
        var household = Household.Create(name, Owner, Now);

        // Assert
        Assert.Equal(HouseholdRole.Owner, household.Find(Owner)!.Role);
    }

    [Fact]
    public void Rename_ShouldFail_WhenTheCallerIsNotAnOwner()
    {
        // Arrange
        var household = AHousehold();
        var renamed = HouseholdName.Create("New name").ShouldBeSuccess();

        // Act
        var result = household.Rename(renamed, actingUserId: Member);

        // Assert
        result.ShouldBeFailure(HouseholdErrors.NotOwner);
        Assert.Equal("Kitchen", household.Name.Value);
    }

    [Fact]
    public void Inherit_ShouldPointAtTheParent_WhenAnOwnerOfTheHeirIsInTheParent()
    {
        // Arrange
        var heir = AHousehold();
        var parent = AHousehold("Parents");

        // Act
        var result = heir.Inherit(parent, [parent.Id], actingUserId: Owner);

        // Assert
        result.ShouldBeSuccess();
        Assert.Equal(parent.Id, heir.InheritsFrom);
    }

    [Fact]
    public void Inherit_ShouldFail_WhenTheCallerIsOnlyAMemberOfTheHeir()
    {
        // Arrange
        var heir = AHousehold();
        var parent = AHousehold("Parents");

        // Act
        var result = heir.Inherit(parent, [parent.Id], actingUserId: Member);

        // Assert
        result.ShouldBeFailure(HouseholdErrors.NotOwner);
        Assert.Null(heir.InheritsFrom);
    }

    [Fact]
    public void Inherit_ShouldSayTheParentDoesNotExist_WhenTheCallerIsNotInIt()
    {
        // Arrange
        var heir = AHousehold();
        var strangers = Household.Create(HouseholdName.Create("Strangers").ShouldBeSuccess(), Stranger, Now);

        // Act
        var result = heir.Inherit(strangers, [strangers.Id], actingUserId: Owner);

        // Assert
        // Inheriting shows the parent's recipes to everybody in the heir, so
        // only somebody who can already see them may do it, and a stranger to
        // the parent learns nothing about whether it exists.
        result.ShouldBeFailure(HouseholdErrors.NotFound(strangers.Id));
        Assert.Null(heir.InheritsFrom);
    }

    [Fact]
    public void Inherit_ShouldFail_WhenTheHouseholdWouldInheritFromItself()
    {
        // Arrange
        var household = AHousehold();

        // Act
        var result = household.Inherit(household, [household.Id], actingUserId: Owner);

        // Assert
        result.ShouldBeFailure(HouseholdErrors.InheritanceCycle);
    }

    [Fact]
    public void Inherit_ShouldFail_WhenTheParentAlreadySeesTheHeirsRecipes()
    {
        // Arrange
        var heir = AHousehold();
        var parent = AHousehold("Parents");
        var grandparentLibrary = new[] { parent.Id, Guid.CreateVersion7(), heir.Id };

        // Act
        var result = heir.Inherit(parent, grandparentLibrary, actingUserId: Owner);

        // Assert
        // A loop would leave "whose recipe is this" with no answer.
        result.ShouldBeFailure(HouseholdErrors.InheritanceCycle);
        Assert.Null(heir.InheritsFrom);
    }

    [Fact]
    public void StopInheriting_ShouldFail_WhenTheCallerIsOnlyAMember()
    {
        // Arrange
        var heir = AHousehold();
        var parent = AHousehold("Parents");
        heir.Inherit(parent, [parent.Id], actingUserId: Owner).ShouldBeSuccess();

        // Act
        var result = heir.StopInheriting(actingUserId: Member);

        // Assert
        result.ShouldBeFailure(HouseholdErrors.NotOwner);
        Assert.Equal(parent.Id, heir.InheritsFrom);
    }

    private static Household AHousehold(string name = "Kitchen")
    {
        var household = Household.Create(
            HouseholdName.Create(name).ShouldBeSuccess(),
            Owner,
            Now);

        household.Add(Member, HouseholdRole.Member, Now).ShouldBeSuccess();

        return household;
    }
}
