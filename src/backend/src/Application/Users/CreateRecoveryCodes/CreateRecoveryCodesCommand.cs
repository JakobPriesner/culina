using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;
using Domain.Users;
using Response = Contracts.Users.CreateRecoveryCodes.Response;

namespace Application.Users.CreateRecoveryCodes;

/// <summary>Makes a new set of recovery codes, replacing any earlier set.</summary>
/// <param name="UserId">Who is asking.</param>
/// <param name="Password">Their password, to prove it is them.</param>
/// <param name="IpAddress">The client address, which decides whose attempt budget is used.</param>
public sealed record CreateRecoveryCodesCommand(Guid UserId, string Password, string? IpAddress);

internal sealed class CreateRecoveryCodesCommandHandler(
    IUserRepository users,
    IRecoveryCodeRepository recoveryCodes,
    ISecretTokens tokens,
    PasswordConfirmation confirmation,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<CreateRecoveryCodesCommand, Response>
{
    public async Task<Result<Response>> Handle(
        CreateRecoveryCodesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Users.CreateRecoveryCodes");

        var found = await users.FindAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        var confirmed = await found.Match(
            async user => (await confirmation
                    .ConfirmAsync(user, command.Password, command.IpAddress, cancellationToken)
                    .ConfigureAwait(false))
                .Map(() => user),
            error => Task.FromResult(Result<User>.Failure(error))).ConfigureAwait(false);

        var result = await confirmed.Match(
            user => CreateAsync(user, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> CreateAsync(User user, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var codes = Enumerable.Range(0, RecoveryCode.SetSize).Select(_ => tokens.NewRecoveryCode()).ToList();

        var saved = codes
            .Select(code => RecoveryCode.Saved(user.Id, tokens.Digest(RecoveryCode.Normalise(code)), now))
            .ToList();

        await unitOfWork.InTransactionAsync(
            async token =>
            {
                await recoveryCodes.ReplaceSavedAsync(user.Id, saved, token).ConfigureAwait(false);

                return Result.Success();
            },
            cancellationToken).ConfigureAwait(false);

        // The codes leave here and are never recoverable: only their digests
        // are stored, so this response is the one chance to show them.
        return new Response { Codes = codes, CreatedAt = now };
    }
}
