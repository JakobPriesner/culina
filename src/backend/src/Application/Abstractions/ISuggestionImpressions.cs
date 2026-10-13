namespace Application.Abstractions;

/// <summary>Remembers what the front page showed somebody, so tomorrow's list can lean away from it.</summary>
public interface ISuggestionImpressions
{
    /// <summary>Notes that these recipes were shown to this person on this day; forgets what is older than the retention.</summary>
    /// <param name="userId">Who saw them.</param>
    /// <param name="recipeIds">What was on screen; any that is not in the library is ignored.</param>
    /// <param name="library">The households whose recipes this person sees, so a reported id cannot name somebody else's recipe.</param>
    /// <param name="day">The day it was shown.</param>
    /// <param name="cancellationToken">Cancels the work when the caller goes away.</param>
    /// <remarks>Idempotent: the list is fixed for the day, so a refresh or a second device writes nothing new.</remarks>
    Task RecordAsync(
        Guid userId,
        IReadOnlyCollection<Guid> recipeIds,
        IReadOnlyCollection<Guid> library,
        DateOnly day,
        CancellationToken cancellationToken);
}
