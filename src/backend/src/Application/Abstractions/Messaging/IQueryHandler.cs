using Domain.Shared;

namespace Application.Abstractions.Messaging;

/// <summary>Handles a query, which reads and never changes observable state.</summary>
/// <remarks>
/// A <c>GET</c> endpoint never dispatches a command: that is what makes exempting safe HTTP methods
/// from the CSRF check sound.
/// </remarks>
public interface IQueryHandler<in TQuery, TResponse>
    where TResponse : notnull
{
    /// <summary>Answers the query.</summary>
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
