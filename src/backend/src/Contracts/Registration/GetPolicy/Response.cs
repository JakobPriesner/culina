namespace Contracts.Registration.GetPolicy;

/// <summary>
/// What a sign-up form needs to know before it draws itself.
/// </summary>
/// <remarks>
/// Public, and deliberately narrower than the administrator's view of the same
/// settings: it says what a prospective account may do, not how the instance is
/// configured. There is no user limit here, and no other setting.
/// </remarks>
public sealed record Response
{
    /// <summary>Whether anyone may create an account.</summary>
    public required bool OpenRegistration { get; init; }

    /// <summary>Whether a new account must present an invitation code.</summary>
    public required bool RequireInvitation { get; init; }

    /// <summary>
    /// Whether this instance has been set up yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// False on a fresh deployment, which lets the sign-up form ask the one
    /// question that only ever applies to the very first account — what to call
    /// the household it creates — instead of showing everyone a field that is
    /// silently ignored.
    /// </para>
    /// <para>
    /// It does reveal that an instance is unclaimed. That is not a new
    /// exposure: the first account is always allowed to register, so an
    /// unclaimed instance is claimable whether or not it says so.
    /// </para>
    /// </remarks>
    public required bool HasAccounts { get; init; }
}
