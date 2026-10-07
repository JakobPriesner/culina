using Domain.Households;
using TestSupport;

namespace Domain.UnitTests.Households;

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
        // Not "forbidden": that would confirm the household exists.
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
        // Even voluntarily: an ownerless household can never be administered again.
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

    [Theory]
    [MemberData(nameof(AnyTarget))]
    public void Remove_ShouldSayTheHouseholdDoesNotExist_ToAStranger_WhoeverTheyName(Guid target)
    {
        // Arrange
        var household = AHousehold();

        // Act
        var result = household.Remove(target, actingUserId: Stranger);

        // Assert
        // One answer whoever is named, so a stranger learns neither that it exists nor who is in it.
        result.ShouldBeFailure(HouseholdErrors.NotFound(household.Id));
    }

    [Theory]
    [MemberData(nameof(AnyTarget))]
    public void ChangeRole_ShouldSayTheHouseholdDoesNotExist_ToAStranger_WhoeverTheyName(Guid target)
    {
        // Arrange
        var household = AHousehold();

        // Act
        var result = household.ChangeRole(target, HouseholdRole.Owner, actingUserId: Stranger);

        // Assert
        result.ShouldBeFailure(HouseholdErrors.NotFound(household.Id));
    }

    [Fact]
    public void ChangeRole_ShouldSayNotOwner_ToAPlainMember_EvenForSomebodyWhoIsNotInIt()
    {
        // Arrange
        var household = AHousehold();

        // Act
        var result = household.ChangeRole(Stranger, HouseholdRole.Owner, actingUserId: Member);

        // Assert
        result.ShouldBeFailure(HouseholdErrors.NotOwner);
    }

    public static TheoryData<Guid> AnyTarget() => [Owner, Member, Stranger, Guid.CreateVersion7()];

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
    public void Inherit_ShouldRememberWhoSetIt_SoTheLinkCanEndWithTheirMembershipOfTheParent()
    {
        // Arrange
        var heir = AHousehold();
        var parent = AHousehold("Parents");

        // Act
        heir.Inherit(parent, [parent.Id], actingUserId: Owner).ShouldBeSuccess();

        // Assert
        Assert.Equal(Owner, heir.InheritsSetBy);
    }

    [Fact]
    public void StopInheriting_ShouldForgetWhoSetIt()
    {
        // Arrange
        var heir = AHousehold();
        var parent = AHousehold("Parents");
        heir.Inherit(parent, [parent.Id], actingUserId: Owner).ShouldBeSuccess();

        // Act
        var result = heir.StopInheriting(actingUserId: Owner);

        // Assert
        // A link is both halves or neither.
        result.ShouldBeSuccess();
        Assert.Null(heir.InheritsFrom);
        Assert.Null(heir.InheritsSetBy);
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
        // Only someone who can already see the parent's recipes may expose them; a stranger learns nothing.
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

    [Fact]
    public void StopInheritingFrom_ShouldLetAnOwnerOfTheParentCutAnHeirLoose()
    {
        // Arrange
        var parent = AHousehold("Parents");
        var heir = Household.Restore(
            Guid.CreateVersion7(),
            HouseholdName.Create("Flat").ShouldBeSuccess(),
            Now,
            version: 1,
            [new HouseholdMember(Stranger, HouseholdRole.Owner, Now)],
            inheritsFrom: parent.Id,
            inheritsSetBy: Stranger);

        // Act
        var result = heir.StopInheritingFrom(parent, actingUserId: Owner);

        // Assert
        // The parent's owner keeps a say over who reads it without being in the heir.
        result.ShouldBeSuccess();
        Assert.Null(heir.InheritsFrom);
        Assert.Null(heir.InheritsSetBy);
    }

    [Fact]
    public void StopInheritingFrom_ShouldFail_WhenTheCallerIsOnlyAMemberOfTheParent()
    {
        // Arrange
        var parent = AHousehold("Parents");
        var heir = Household.Restore(Guid.CreateVersion7(), HouseholdName.Create("Flat").ShouldBeSuccess(), Now, 1, [], parent.Id, Stranger);

        // Act
        var result = heir.StopInheritingFrom(parent, actingUserId: Member);

        // Assert
        result.ShouldBeFailure(HouseholdErrors.NotOwner);
        Assert.Equal(parent.Id, heir.InheritsFrom);
    }

    [Fact]
    public void StopInheritingFrom_ShouldSayTheHeirDoesNotExist_WhenItDoesNotInheritFromThisOne()
    {
        // Arrange
        var parent = AHousehold("Parents");
        var elsewhere = Guid.CreateVersion7();
        var heir = Household.Restore(Guid.CreateVersion7(), HouseholdName.Create("Flat").ShouldBeSuccess(), Now, 1, [], elsewhere, Stranger);

        // Act
        var result = heir.StopInheritingFrom(parent, actingUserId: Owner);

        // Assert
        // Inheriting through another household is that household's to cut.
        result.ShouldBeFailure(HouseholdErrors.NotFound(heir.Id));
        Assert.Equal(elsewhere, heir.InheritsFrom);
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
