using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;
using Domain.Users;
using Response = Contracts.Users.Register.Response;

namespace Application.Users.Register;

/// <summary>Creates an account.</summary>
/// <param name="Email">The address to sign in with.</param>
/// <param name="DisplayName">What to call them.</param>
/// <param name="Password">Their chosen password.</param>
/// <param name="HouseholdName">What to call the first household, if one is created.</param>
/// <param name="InvitationCode">A code that joins the new account to a household.</param>
public sealed record RegisterUserCommand(
    string Email,
    string DisplayName,
    string Password,
    string? HouseholdName,
    string? InvitationCode);

internal sealed class RegisterUserCommandHandler(
    IUserRepository users,
    IHouseholdRepository households,
    IInvitationRepository invitations,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    RegistrationDependencies dependencies)
    : ICommandHandler<RegisterUserCommand, Response>
{
    public async Task<Result<Response>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Users.Register");

        var existing = await users.CountAsync(cancellationToken).ConfigureAwait(false);
        var isFirstAccount = existing == 0;

        var accepted = MayRegister(isFirstAccount, existing).Bind(() => Validate(command));

        var result = await accepted.Match(
            account => StoreAsync(account, isFirstAccount, command, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    /// <summary>
    /// The first account is always allowed: a fresh instance has to have a way
    /// in, and that account becomes the administrator who can then open
    /// registration to everyone else.
    /// </summary>
    private Result MayRegister(bool isFirstAccount, int existingUsers)
    {
        if (isFirstAccount)
        {
            return Result.Success();
        }

        if (!dependencies.Registration.OpenRegistration)
        {
            return UserErrors.RegistrationClosed;
        }

        return existingUsers >= dependencies.Registration.MaxUsers
            ? UserErrors.MaxUsersReached
            : Result.Success();
    }

    /// <summary>
    /// Checks every field in one pass, so the form can mark all of its wrong
    /// inputs at once instead of one per submit.
    /// </summary>
    private static Result<NewAccount> Validate(RegisterUserCommand command)
    {
        var email = Email.Create(command.Email);
        var displayName = DisplayName.Create(command.DisplayName);

        return Result.Combine(
                Labelled(email, "email"),
                Labelled(displayName, "displayName"),
                Labelled(User.EnsureAcceptablePassword(command.Password), "password"))
            .Bind(() => email.Bind(address => displayName
                .Map(name => new NewAccount(address, name, command.Password))));
    }

    private async Task<Result<Response>> StoreAsync(
        NewAccount account,
        bool isFirstAccount,
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        var user = User.Register(
            account.Email,
            account.DisplayName,
            passwordHasher.Hash(account.Password),
            dependencies.Time.GetUtcNow());

        // The account and the household it lands in are one atomic step: an
        // account with neither a household nor a way to get one is a dead end,
        // and a consumed invitation with no account behind it is worse.
        return await unitOfWork.InTransactionAsync(
            async token =>
            {
                var added = await users.AddAsync(user, isFirstAccount, token).ConfigureAwait(false);

                return await added.Match(
                    () => PlaceAsync(user, isFirstAccount, command, token),
                    error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives the new account somewhere to cook: its own household for the very
    /// first user, or the household the invitation admits to.
    /// </summary>
    private async Task<Result<Response>> PlaceAsync(
        User user,
        bool isFirstAccount,
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        if (isFirstAccount)
        {
            var created = await FirstHouseholdIdAsync(user, true, command.HouseholdName, cancellationToken)
                .ConfigureAwait(false);

            return user.ToRegisterResponse(isAdmin: true, created);
        }

        if (string.IsNullOrWhiteSpace(command.InvitationCode))
        {
            // Allowed only when the policy does not demand a code; the account
            // then starts with no household and the client offers to create one.
            return dependencies.Registration.RequireInvitation
                ? HouseholdErrors.InvitationInvalid
                : user.ToRegisterResponse(isAdmin: false, householdId: null);
        }

        return await JoinByInvitationAsync(user, command.InvitationCode, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<Response>> JoinByInvitationAsync(
        User user,
        string code,
        CancellationToken cancellationToken)
    {
        var found = await invitations.FindByCodeAsync(code, cancellationToken).ConfigureAwait(false);

        var redeemed = found.Bind(invitation => invitation
            .Redeem(user.Id, dependencies.Time.GetUtcNow())
            .Map(() => invitation));

        return await redeemed.Match(
            invitation => AddToHouseholdAsync(user, invitation, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
    }

    private async Task<Result<Response>> AddToHouseholdAsync(
        User user,
        HouseholdInvitation invitation,
        CancellationToken cancellationToken)
    {
        var marked = await invitations.MarkRedeemedAsync(invitation, cancellationToken)
            .ConfigureAwait(false);
        var household = await households.FindAsync(invitation.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        var joined = marked.Bind(() => household)
            .Bind(found => found
                .Add(user.Id, HouseholdRole.Member, user.CreatedAt)
                .Map(() => found));

        return await joined.Match(
            async found =>
            {
                var saved = await households
                    .UpdateAsync(found, found.Version, cancellationToken)
                    .ConfigureAwait(false);

                return saved.Map(_ => user.ToRegisterResponse(isAdmin: false, found.Id));
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
    }

    private async Task<Guid?> FirstHouseholdIdAsync(
        User user,
        bool isFirstAccount,
        string? householdName,
        CancellationToken cancellationToken)
    {
        if (!isFirstAccount)
        {
            return null;
        }

        // A blank or over-long household name is not worth failing a
        // registration over: a household can be renamed, a failed sign-up
        // cannot be undone.
        var name = HouseholdName.Create(householdName)
            .Match(value => value, _ => DefaultHouseholdName(user));

        var household = Household.Create(name, user.Id, user.CreatedAt);

        await households.AddAsync(household, cancellationToken).ConfigureAwait(false);

        return household.Id;
    }

    private static HouseholdName DefaultHouseholdName(User user) =>
        HouseholdName.Create($"{user.DisplayName.Value}'s kitchen").Match(
            value => value,
            // The display name is already bounded, so the fallback is always
            // valid; a fixed name keeps this total rather than throwing.
            _ => HouseholdName.Create("Kitchen").Match(value => value, _ => throw new InvalidOperationException(
                "The constant fallback household name failed validation.")));

    /// <summary>
    /// Re-labels a value object's failure with the request field it came from,
    /// so the client can mark the right input.
    /// </summary>
    private static Result Labelled<TValue>(Result<TValue> result, string field) =>
        result.Match(_ => Result.Success(), error => Result.Failure(Label(error, field)));

    private static Result Labelled(Result result, string field) =>
        result.Match(Result.Success, error => Result.Failure(Label(error, field)));

    private static FieldError Label(Error error, string field) =>
        new FieldError(field, error.Code, error.Description);

    /// <summary>The validated inputs, so nothing downstream re-parses them.</summary>
    private sealed record NewAccount(Email Email, DisplayName DisplayName, string Password);
}
