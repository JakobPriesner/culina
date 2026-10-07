using Application.Abstractions;

namespace TestSupport;

/// <summary>The caller a handler sees, supplied as a constructor argument.</summary>
/// <param name="userId">The signed-in user, or null for an anonymous caller.</param>
public sealed class FakeUserContext(Guid? userId = null) : IUserContext
{
    public bool IsAuthenticated => userId.HasValue;

    public Guid UserId => userId
        ?? throw new InvalidOperationException("This fake was built for an anonymous caller.");

    public static FakeUserContext SignedIn() => new(Guid.CreateVersion7());
}
