using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;
using Domain.Users;
using Response = Contracts.Users.UpdateCurrent.Response;

namespace Application.Users.UpdateCurrent;

/// <summary>Renames the signed-in user.</summary>
/// <param name="UserId">Who is asking.</param>
/// <param name="DisplayName">The new name.</param>
/// <param name="ExpectedVersion">The version the caller last saw.</param>
public sealed record UpdateCurrentUserCommand(Guid UserId, string DisplayName, long ExpectedVersion);

internal sealed class UpdateCurrentUserCommandHandler(IUserRepository users)
    : ICommandHandler<UpdateCurrentUserCommand, Response>
{
    public async Task<Result<Response>> Handle(
        UpdateCurrentUserCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Users.UpdateCurrent");

        var name = DisplayName.Create(command.DisplayName);
        var found = await users.FindAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        var prepared = name.Bind(value => found.Map(user => (User: user, Name: value)));

        var result = await prepared.Match(
            pair => SaveAsync(pair.User, pair.Name, command.ExpectedVersion, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> SaveAsync(
        User user,
        DisplayName name,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        user.ChangeDisplayName(name);

        var saved = await users.UpdateAsync(user, expectedVersion, cancellationToken).ConfigureAwait(false);

        return saved.Map(version => new Response
        {
            UserId = user.Id,
            DisplayName = user.DisplayName.Value,
            Version = version
        });
    }
}
