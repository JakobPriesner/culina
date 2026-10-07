namespace Contracts.Households.GetInvitationByCode;

/// <summary>What an invitation code leads to, before it is used.</summary>
/// <remarks>
/// The name and nothing else: enough for somebody to recognise whose kitchen
/// they are being asked into, and nothing a code holder could not learn by
/// joining.
/// </remarks>
public sealed record Response
{
    /// <summary>The name of the household the code admits to.</summary>
    public required string HouseholdName { get; init; }
}
