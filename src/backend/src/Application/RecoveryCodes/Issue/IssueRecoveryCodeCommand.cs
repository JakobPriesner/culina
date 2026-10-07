using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;
using Domain.Users;
using Response = Contracts.RecoveryCodes.Issue.Response;

namespace Application.RecoveryCodes.Issue;

/// <summary>Issues a one-time code for somebody who is locked out; administrator only.</summary>
/// <remarks>The administrator is told whether the address is registered: they can see every account anyway, and a code for nobody would look like a working one.</remarks>
public sealed record IssueRecoveryCodeCommand(Guid AdministratorId, string Email);

internal sealed class IssueRecoveryCodeCommandHandler(
    IUserRepository users,
    IRecoveryCodeRepository recoveryCodes,
    ISecretTokens tokens,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<IssueRecoveryCodeCommand, Response>
{
    public async Task<Result<Response>> Handle(
        IssueRecoveryCodeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("RecoveryCodes.Issue");

        var found = await Email.Create(command.Email).Match(
            email => users.FindByEmailAsync(email, cancellationToken),
            error => Task.FromResult(Result<User>.Failure(error))).ConfigureAwait(false);

        var result = await found.Match(
            user => IssueAsync(user, command.AdministratorId, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> IssueAsync(
        User user,
        Guid administratorId,
        CancellationToken cancellationToken)
    {
        var code = tokens.NewRecoveryCode();
        var issued = RecoveryCode.Issued(
            user.Id,
            tokens.Digest(RecoveryCode.Normalise(code)),
            administratorId,
            time.GetUtcNow());

        await unitOfWork.InTransactionAsync(
            async token =>
            {
                await recoveryCodes.AddAsync(issued, token).ConfigureAwait(false);

                return Result.Success();
            },
            cancellationToken).ConfigureAwait(false);

        return new Response { Code = code, ExpiresAt = issued.ExpiresAt!.Value };
    }
}
