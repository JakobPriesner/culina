namespace Contracts.Households.GetInvitationByCode;

/// <summary>
/// What an invitation code leads to, before it is used: the name only, enough to recognise whose
/// kitchen it is and nothing a code holder could not learn by joining.
/// </summary>
public sealed record Response
{
    /// <summary>The name of the household the code admits to.</summary>
    public required string HouseholdName { get; init; }
}
