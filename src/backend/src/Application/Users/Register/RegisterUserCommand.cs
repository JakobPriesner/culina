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
public sealed record RegisterUserCommand(
    string Email,
    string DisplayName,
    string Password,
    string? HouseholdName);

internal sealed class RegisterUserCommandHandler(
    RegistrationSettings registration,
    IUserRepository users,
    IHouseholdRepository households,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider time)
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
            account => StoreAsync(account, isFirstAccount, command.HouseholdName, cancellationToken),
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

        if (!registration.OpenRegistration)
        {
            return UserErrors.RegistrationClosed;
        }

        // Invitation-gated registration is not implemented yet. Until the
        // invitation endpoints exist, requiring a code means registration
        // genuinely is closed, and saying so is better than quietly accepting
        // an account the admin did not intend to allow.
        if (registration.RequireInvitation)
        {
            return UserErrors.RegistrationClosed;
        }

        return existingUsers >= registration.MaxUsers
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
        string? householdName,
        CancellationToken cancellationToken)
    {
        var user = User.Register(
            account.Email,
            account.DisplayName,
            passwordHasher.Hash(account.Password),
            time.GetUtcNow());

        // The account and its first household are one atomic step: an admin
        // account with no household would land on a dead end.
        return await unitOfWork.InTransactionAsync(
            async token =>
            {
                var added = await users.AddAsync(user, isFirstAccount, token).ConfigureAwait(false);

                return await added.Match(
                    async () => Result<Response>.Success(user.ToRegisterResponse(
                        isFirstAccount,
                        await FirstHouseholdIdAsync(user, isFirstAccount, householdName, token)
                            .ConfigureAwait(false))),
                    error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
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
