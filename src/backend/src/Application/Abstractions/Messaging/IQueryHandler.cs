using Domain.Shared;

namespace Application.Abstractions.Messaging;

/// <summary>
/// Handles a query, which reads and never changes observable state.
/// </summary>
/// <typeparam name="TQuery">The query this handler accepts.</typeparam>
/// <typeparam name="TResponse">The API-shaped response it produces.</typeparam>
/// <remarks>
/// A <c>GET</c> endpoint never dispatches a command. That separation is what
/// makes exempting safe HTTP methods from the CSRF check sound.
/// </remarks>
public interface IQueryHandler<in TQuery, TResponse>
{
    /// <summary>Answers the query.</summary>
    /// <param name="query">What to read.</param>
    /// <param name="cancellationToken">Cancels the work when the caller goes away.</param>
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
