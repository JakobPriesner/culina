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

        // A first look so a policy refusal costs no Argon2 run; the count is re-read under the registration lock.
        var existing = await users.CountAsync(cancellationToken).ConfigureAwait(false);

        var accepted = MayRegister(existing, command.InvitationCode).Bind(() => Validate(command));

        var result = await accepted.Match(
            account => StoreAsync(account, command, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    // The first account is always allowed (a fresh instance needs a way in) and becomes the administrator.
    private Result MayRegister(int existingUsers, string? invitationCode)
    {
        if (existingUsers == 0)
        {
            return Result.Success();
        }

        if (!dependencies.Registration.OpenRegistration)
        {
            return UserErrors.RegistrationClosed;
        }

        if (existingUsers >= dependencies.Registration.MaxUsers)
        {
            return UserErrors.MaxUsersReached;
        }

        // Refused before the address is looked at, so an invite-only instance reveals nothing about accounts.
        return dependencies.Registration.RequireInvitation && string.IsNullOrWhiteSpace(invitationCode)
            ? HouseholdErrors.InvitationInvalid
            : Result.Success();
    }

    // One pass over every field so the form can mark all wrong inputs at once.
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
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        // Hashed before the transaction so the registration lock is held for a few statements, not an Argon2 run.
        var user = User.Register(
            account.Email,
            account.DisplayName,
            await passwordHasher.HashAsync(account.Password, cancellationToken).ConfigureAwait(false),
            dependencies.Time.GetUtcNow());

        // Account and household are one atomic step: neither a dead-end account nor a consumed invitation without one.
        return await unitOfWork.InTransactionAsync(
            async token =>
            {
                // Counted under the lock, or every registration on an empty instance would become an administrator.
                var existing = await users.CountForRegistrationAsync(token).ConfigureAwait(false);

                var admitted = await MayRegister(existing, command.InvitationCode).Match(
                    () => AdmitAsync(existing == 0, command.InvitationCode, token),
                    error => Task.FromResult(Result<Admission>.Failure(error))).ConfigureAwait(false);

                return await admitted.Match(
                    admission => AddAsync(user, admission, command.HouseholdName, token),
                    error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    // Writing the account reports an already-registered address, so a bad invitation is refused first:
    // that answer only reaches somebody this instance would let in.
    private async Task<Result<Admission>> AdmitAsync(
        bool isFirstAccount,
        string? invitationCode,
        CancellationToken cancellationToken)
    {
        if (isFirstAccount || string.IsNullOrWhiteSpace(invitationCode))
        {
            return new Admission(isFirstAccount, Invitation: null);
        }

        var found = await invitations.FindByCodeAsync(invitationCode, cancellationToken).ConfigureAwait(false);

        return found.Bind(invitation => invitation.IsUsable(dependencies.Time.GetUtcNow())
            ? Result<Admission>.Success(new Admission(IsFirstAccount: false, invitation))
            : HouseholdErrors.InvitationInvalid);
    }

    private async Task<Result<Response>> AddAsync(
        User user,
        Admission admission,
        string? householdName,
        CancellationToken cancellationToken)
    {
        var added = await users.AddAsync(user, admission.IsFirstAccount, cancellationToken).ConfigureAwait(false);

        return await added.Match(
            () => PlaceAsync(user, admission, householdName, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
    }

    // Gives the new account its own household (first user) or the one the invitation admits to.
    private async Task<Result<Response>> PlaceAsync(
        User user,
        Admission admission,
        string? householdName,
        CancellationToken cancellationToken)
    {
        if (admission.IsFirstAccount)
        {
            var created = await FirstHouseholdIdAsync(user, householdName, cancellationToken)
                .ConfigureAwait(false);

            return user.ToRegisterResponse(isAdmin: true, created);
        }

        if (admission.Invitation is null)
        {
            // No code needed by policy: the account starts with no household and the client offers to create one.
            return user.ToRegisterResponse(isAdmin: false, householdId: null);
        }

        return await admission.Invitation.Redeem(user.Id, user.CreatedAt).Match(
            () => AddToHouseholdAsync(user, admission.Invitation, cancellationToken),
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

    private async Task<Guid> FirstHouseholdIdAsync(
        User user,
        string? householdName,
        CancellationToken cancellationToken)
    {
        // A bad household name is not worth failing registration over: it can be renamed.
        var name = HouseholdName.Create(householdName)
            .Match(value => value, _ => DefaultHouseholdName(user));

        var household = Household.Create(name, user.Id, user.CreatedAt);

        await households.AddAsync(household, cancellationToken).ConfigureAwait(false);

        return household.Id;
    }

    private static HouseholdName DefaultHouseholdName(User user) =>
        HouseholdName.Create($"{user.DisplayName.Value}'s kitchen").Match(
            value => value,
            // The display name is already bounded, so the fixed fallback is always valid.
            _ => HouseholdName.Create("Kitchen").Match(value => value, _ => throw new InvalidOperationException(
                "The constant fallback household name failed validation.")));

    // Re-labels a value object's failure with the request field it came from.
    private static Result Labelled<TValue>(Result<TValue> result, string field)
        where TValue : notnull =>
        result.Match(_ => Result.Success(), error => Result.Failure(Label(error, field)));

    private static Result Labelled(Result result, string field) =>
        result.Match(Result.Success, error => Result.Failure(Label(error, field)));

    private static FieldError Label(Error error, string field) =>
        new FieldError(field, error.Code, error.Description);

    private sealed record NewAccount(Email Email, DisplayName DisplayName, string Password);

    // What the policy decided, before anything was written.
    private sealed record Admission(bool IsFirstAccount, HouseholdInvitation? Invitation);
}
