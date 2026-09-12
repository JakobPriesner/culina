using Domain.Shared;

namespace Application.Abstractions.Messaging;

/// <summary>
/// Handles a command that changes state and returns no value.
/// </summary>
/// <typeparam name="TCommand">The command this handler accepts.</typeparam>
/// <remarks>
/// There is no mediator: an endpoint injects this interface directly as a
/// delegate parameter. That keeps the call graph navigable from the route to
/// the handler and costs one explicit DI registration per handler.
/// </remarks>
public interface ICommandHandler<in TCommand>
{
    /// <summary>Executes the command.</summary>
    /// <param name="command">What to do.</param>
    /// <param name="cancellationToken">Cancels the work when the caller goes away.</param>
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Handles a command that changes state and returns a response.
/// </summary>
/// <typeparam name="TCommand">The command this handler accepts.</typeparam>
/// <typeparam name="TResponse">The API-shaped response it produces.</typeparam>
public interface ICommandHandler<in TCommand, TResponse>
    where TResponse : notnull
{
    /// <summary>Executes the command.</summary>
    /// <param name="command">What to do.</param>
    /// <param name="cancellationToken">Cancels the work when the caller goes away.</param>
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}
