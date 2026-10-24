namespace Contracts.Households.CreateInvitation;

/// <summary>Who to invite; the body may be left out entirely to only get a code to share.</summary>
public sealed record Request
{
    /// <summary>An address to mail the invitation to, or null. Needs mail to be set up on the server.</summary>
    public string? Email { get; init; }
}
