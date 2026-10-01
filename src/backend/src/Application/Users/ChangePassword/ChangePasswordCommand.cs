using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;
using Domain.Users;

namespace Application.Users.ChangePassword;

/// <summary>Changes the signed-in user's password.</summary>
/// <param name="UserId">Who is asking.</param>
/// <param name="SessionId">The session they are asking from, which stays signed in.</param>
/// <param name="CurrentPassword">Their password now, to prove it is them.</param>
/// <param name="NewPassword">What it becomes.</param>
public sealed record ChangePasswordCommand(
    Guid UserId,
    Guid SessionId,
    string CurrentPassword,
    string NewPassword);

internal sealed class ChangePasswordCommandHandler(
    IUserRepository users,
    ISessionStore sessions,
    IPasswordHasher passwordHasher,
    PasswordConfirmation confirmation,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<ChangePasswordCommand>
{
    public async Task<Result> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Users.ChangePassword");

        var acceptable = User.EnsureAcceptablePassword(command.NewPassword);
        var found = await users.FindAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        var confirmed = acceptable.Bind(() => found.Bind(user =>
            confirmation.Confirm(user, command.CurrentPassword).Map(() => user)));

        var result = await confirmed.Match(
            user => SaveAsync(user, command, cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private Task<Result> SaveAsync(User user, ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        user.ChangePasswordHash(passwordHasher.Hash(command.NewPassword));

        return unitOfWork.InTransactionAsync(
            async token =>
            {
                var saved = await users.UpdateAsync(user, user.Version, token).ConfigureAwait(false);

                // Every other device is signed out: a password changed because
                // somebody else knew it has to shut that somebody out, and the
                // person changing it is holding the one device that stays.
                await sessions
                    .RevokeAllAsync(user.Id, command.SessionId, time.GetUtcNow(), token)
                    .ConfigureAwait(false);

                return saved.Bind(_ => Result.Success());
            },
            cancellationToken);
    }
}
