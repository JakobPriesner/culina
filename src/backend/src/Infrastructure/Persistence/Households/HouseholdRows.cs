using Domain.Households;

namespace Infrastructure.Persistence.Households;

/// <summary>The <c>households</c> row as PostgreSQL returns it.</summary>
internal sealed record HouseholdRow
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public long Version { get; init; }

    public Guid? InheritsFrom { get; init; }
}

/// <summary>One household in another's chain of inheritance.</summary>
internal sealed record InheritedHouseholdRow
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;
}

/// <summary>The <c>household_members</c> row as PostgreSQL returns it.</summary>
internal sealed record HouseholdMemberRow
{
    public Guid HouseholdId { get; init; }

    public Guid UserId { get; init; }

    public string Role { get; init; } = string.Empty;

    public DateTimeOffset JoinedAt { get; init; }
}

/// <summary>
/// The members-with-names projection as PostgreSQL returns it.
/// </summary>
/// <remarks>
/// A row type with settable properties rather than mapping straight onto the
/// positional read model: Dapper's underscore matching applies to properties,
/// not to constructor parameters, so a positional record silently fails to bind
/// snake_case columns.
/// </remarks>
internal sealed record HouseholdMemberViewRow
{
    public Guid UserId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public DateTimeOffset JoinedAt { get; init; }
}

/// <summary>Turns stored rows back into a domain household.</summary>
internal static class HouseholdRowMappings
{
    internal static Household ToDomain(this HouseholdRow row, IEnumerable<HouseholdMemberRow> members)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(members);

        var name = HouseholdName.Create(row.Name).Match(
            value => value,
            error => throw new InvalidOperationException(
                $"Stored household name is not valid ({error.Code}). The row is corrupt."));

        return Household.Restore(
            row.Id,
            name,
            row.CreatedAt,
            row.Version,
            members.Select(member => member.ToDomain()),
            row.InheritsFrom);
    }

    internal static Application.Abstractions.HouseholdMemberView ToView(this HouseholdMemberViewRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new Application.Abstractions.HouseholdMemberView(
            row.UserId,
            row.DisplayName,
            row.Role,
            row.JoinedAt);
    }

    internal static HouseholdMember ToDomain(this HouseholdMemberRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new HouseholdMember(row.UserId, row.Role.ToRole(), row.JoinedAt);
    }

    /// <summary>
    /// Roles are stored as text rather than as an integer, so a dump is
    /// readable and inserting an enum member later cannot renumber the
    /// existing rows.
    /// </summary>
    internal static string ToStorage(this HouseholdRole role) => role switch
    {
        HouseholdRole.Owner => "owner",
        HouseholdRole.Member => "member",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown household role.")
    };

    private static HouseholdRole ToRole(this string stored) => stored switch
    {
        "owner" => HouseholdRole.Owner,
        "member" => HouseholdRole.Member,
        _ => throw new InvalidOperationException($"Stored household role '{stored}' is not known.")
    };
}
