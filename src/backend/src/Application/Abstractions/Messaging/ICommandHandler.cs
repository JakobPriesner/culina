using Domain.Shared;

namespace Application.Abstractions.Messaging;

/// <summary>Handles a command that changes state and returns no value.</summary>
/// <remarks>
/// No mediator: an endpoint injects this interface directly, keeping the call graph navigable from
/// route to handler at the cost of one DI registration per handler.
/// </remarks>
public interface ICommandHandler<in TCommand>
{
    /// <summary>Executes the command.</summary>
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>Handles a command that changes state and returns a response.</summary>
public interface ICommandHandler<in TCommand, TResponse>
    where TResponse : notnull
{
    /// <summary>Executes the command.</summary>
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}
