using Application.Abstractions;

namespace TestSupport;

/// <summary>
/// The caller a handler sees, supplied as a plain constructor argument.
/// </summary>
/// <remarks>
/// Because every collaborator arrives through the constructor, a test never
/// needs a container to say who is calling.
/// </remarks>
/// <param name="userId">The signed-in user, or null for an anonymous caller.</param>
public sealed class FakeUserContext(Guid? userId = null) : IUserContext
{
    public bool IsAuthenticated => userId.HasValue;

    public Guid UserId => userId
        ?? throw new InvalidOperationException("This fake was built for an anonymous caller.");

    /// <summary>A caller with a fresh, arbitrary identity.</summary>
    public static FakeUserContext SignedIn() => new(Guid.CreateVersion7());
}
