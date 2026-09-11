namespace Domain.Shared;

/// <summary>
/// An error carrying several contributing failures, so one round trip can
/// report every problem with a request instead of one problem per submit.
/// </summary>
public sealed record ValidationError : Error
{
    /// <summary>The code every validation aggregate reports.</summary>
    public const string ValidationCode = "request.validation_failed";

    /// <summary>Creates an aggregate over the given failures.</summary>
    /// <param name="errors">The contributing failures. Must not be empty.</param>
    public ValidationError(IReadOnlyList<Error> errors)
        : base(ValidationCode, "One or more values were rejected.", ErrorType.Validation)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException("A validation error needs at least one cause.", nameof(errors));
        }

        Errors = errors;
    }

    /// <summary>The individual failures. Any <see cref="FieldError"/> among them
    /// becomes an entry in the problem document's <c>errors</c> array.</summary>
    public IReadOnlyList<Error> Errors { get; }
}
