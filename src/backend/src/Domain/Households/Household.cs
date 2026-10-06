using Domain.Shared;

namespace Domain.Households;

/// <summary>
/// The people you cook with, and everything they share.
/// </summary>
/// <remarks>
/// The household is Culina's unit of sharing: recipes, tags and the shopping
/// list belong to it, while notes, cook history and cooking sessions belong to
/// a person. That separation is why two people can disagree about a recipe
/// without either of them editing it.
/// </remarks>
public sealed class Household
{
    private readonly List<HouseholdMember> members;

    private Household(
        Guid id,
        HouseholdName name,
        DateTimeOffset createdAt,
        long version,
        List<HouseholdMember> members,
        Guid? inheritsFrom,
        Guid? inheritsSetBy)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
        Version = version;
        this.members = members;
        InheritsFrom = inheritsFrom;
        InheritsSetBy = inheritsSetBy;
    }

    /// <summary>The household's identifier.</summary>
    public Guid Id { get; }

    /// <summary>What it is called.</summary>
    public HouseholdName Name { get; private set; }

    /// <summary>When it was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Incremented by every write; the ETag is derived from it.</summary>
    public long Version { get; }

    /// <summary>Who is in it.</summary>
    public IReadOnlyList<HouseholdMember> Members => members;

    /// <summary>
    /// The household whose recipes this one sees but does not edit, if any.
    /// </summary>
    public Guid? InheritsFrom { get; private set; }

    /// <summary>
    /// Who set <see cref="InheritsFrom"/>: a member of that household, whose
    /// recipes they chose to show here.
    /// </summary>
    /// <remarks>
    /// The link lasts only as long as their membership of the parent. When
    /// they leave it, or are shown out, the database drops the link with the
    /// membership, so nobody keeps reading a kitchen through a household of
    /// their own after that kitchen has let them go.
    /// </remarks>
    public Guid? InheritsSetBy { get; private set; }

    /// <summary>Creates a household with its first owner.</summary>
    /// <param name="name">The validated name.</param>
    /// <param name="ownerId">The person creating it.</param>
    /// <param name="createdAt">The injected current time.</param>
    public static Household Create(HouseholdName name, Guid ownerId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(name);

        return new Household(
            CulinaId.New(),
            name,
            createdAt,
            version: 1,
            [new HouseholdMember(ownerId, HouseholdRole.Owner, createdAt)],
            inheritsFrom: null,
            inheritsSetBy: null);
    }

    /// <summary>Rebuilds a household from storage.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="name">Its name.</param>
    /// <param name="createdAt">When it was created.</param>
    /// <param name="version">The stored version.</param>
    /// <param name="members">Its members.</param>
    /// <param name="inheritsFrom">The household it inherits recipes from, if any.</param>
    /// <param name="inheritsSetBy">Who set <paramref name="inheritsFrom"/>, if anybody.</param>
    public static Household Restore(
        Guid id,
        HouseholdName name,
        DateTimeOffset createdAt,
        long version,
        IEnumerable<HouseholdMember> members,
        Guid? inheritsFrom,
        Guid? inheritsSetBy)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(members);

        return new Household(id, name, createdAt, version, [.. members], inheritsFrom, inheritsSetBy);
    }

    /// <summary>Renames the household. Owners only.</summary>
    /// <param name="name">The new validated name.</param>
    /// <param name="actingUserId">Who is asking.</param>
    public Result Rename(HouseholdName name, Guid actingUserId)
    {
        ArgumentNullException.ThrowIfNull(name);

        return HouseholdMembershipPolicy.CanAdminister(this, actingUserId)
            .Tap(() => Name = name);
    }

    /// <summary>
    /// Sees every recipe another household sees, from now on. Owners only.
    /// </summary>
    /// <param name="parent">The household to inherit from.</param>
    /// <param name="parentLibrary">
    /// Every household whose recipes <paramref name="parent"/> sees, itself
    /// included — what the cycle check needs to look through.
    /// </param>
    /// <param name="actingUserId">Who is asking.</param>
    public Result Inherit(Household parent, IReadOnlyCollection<Guid> parentLibrary, Guid actingUserId)
    {
        ArgumentNullException.ThrowIfNull(parent);

        return HouseholdMembershipPolicy.CanInherit(this, parent, parentLibrary, actingUserId)
            .Tap(() =>
            {
                InheritsFrom = parent.Id;
                InheritsSetBy = actingUserId;
            });
    }

    /// <summary>Stops inheriting anybody's recipes. Owners only.</summary>
    /// <param name="actingUserId">Who is asking.</param>
    public Result StopInheriting(Guid actingUserId) =>
        HouseholdMembershipPolicy.CanAdminister(this, actingUserId)
            .Tap(ForgetInheritance);

    /// <summary>
    /// Stops inheriting from a household whose owner has asked it to.
    /// </summary>
    /// <param name="parent">The household it inherits from.</param>
    /// <param name="actingUserId">An owner of <paramref name="parent"/>.</param>
    /// <remarks>
    /// The other half of <see cref="Inherit"/>. Inheriting is set up from the
    /// heir's side, by somebody who can see both kitchens; this is how the
    /// kitchen being read keeps a say over who reads it.
    /// </remarks>
    public Result StopInheritingFrom(Household parent, Guid actingUserId)
    {
        ArgumentNullException.ThrowIfNull(parent);

        return HouseholdMembershipPolicy.CanCutInheritance(this, parent, actingUserId)
            .Tap(ForgetInheritance);
    }

    /// <summary>Adds someone to the household.</summary>
    /// <param name="userId">Who is joining.</param>
    /// <param name="role">What they may do.</param>
    /// <param name="joinedAt">The injected current time.</param>
    public Result Add(Guid userId, HouseholdRole role, DateTimeOffset joinedAt)
    {
        if (Find(userId) is not null)
        {
            return HouseholdErrors.AlreadyAMember;
        }

        members.Add(new HouseholdMember(userId, role, joinedAt));

        return Result.Success();
    }

    /// <summary>Removes someone from the household.</summary>
    /// <param name="userId">Who is leaving or being removed.</param>
    /// <param name="actingUserId">Who is asking.</param>
    public Result Remove(Guid userId, Guid actingUserId) =>
        HouseholdMembershipPolicy.CanRemove(this, userId, actingUserId)
            .Tap(() => members.RemoveAll(member => member.UserId == userId));

    /// <summary>Changes what a member may do.</summary>
    /// <param name="userId">Whose role changes.</param>
    /// <param name="role">The new role.</param>
    /// <param name="actingUserId">Who is asking.</param>
    public Result ChangeRole(Guid userId, HouseholdRole role, Guid actingUserId) =>
        HouseholdMembershipPolicy.CanChangeRole(this, userId, role, actingUserId)
            .Tap(() =>
            {
                var index = members.FindIndex(member => member.UserId == userId);

                members[index] = members[index] with { Role = role };
            });

    /// <summary>The membership record for a user, or null when they are not a member.</summary>
    /// <param name="userId">Who to look for.</param>
    public HouseholdMember? Find(Guid userId) =>
        members.Find(member => member.UserId == userId);

    /// <summary>How many owners the household has.</summary>
    public int OwnerCount => members.Count(member => member.Role == HouseholdRole.Owner);

    private void ForgetInheritance()
    {
        InheritsFrom = null;
        InheritsSetBy = null;
    }
}
