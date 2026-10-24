namespace Contracts.EmailConfirmations.Create;

/// <summary>The token from a link Culina mailed.</summary>
public sealed record Request
{
    /// <summary>What follows <c>#token=</c> in the link.</summary>
    public required string Token { get; init; }
}
