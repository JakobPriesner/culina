namespace Domain.Shared;

/// <summary>
/// Collapses several failures into the one error a caller should see.
/// </summary>
internal static class ErrorAggregate
{
    /// <summary>
    /// Returns a single failure unchanged, and wraps two or more in a
    /// <see cref="ValidationError"/>.
    /// </summary>
    /// <remarks>
    /// A lone failure is passed through so that combining one check cannot turn
    /// a <see cref="ErrorType.Conflict"/> or <see cref="ErrorType.NotFound"/>
    /// into a 400. Nested aggregates are flattened, so combining combined
    /// results still yields one flat list for the problem document.
    /// </remarks>
    internal static Error Of(IReadOnlyList<Error> errors)
    {
        if (errors.Count == 1)
        {
            return errors[0];
        }

        List<Error> flattened = [];

        foreach (var error in errors)
        {
            if (error is ValidationError aggregate)
            {
                flattened.AddRange(aggregate.Errors);
            }
            else
            {
                flattened.Add(error);
            }
        }

        return new ValidationError(flattened);
    }
}
