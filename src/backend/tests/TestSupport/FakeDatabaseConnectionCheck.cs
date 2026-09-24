using Application.Abstractions.Settings;
using Domain.Shared;

namespace TestSupport;

/// <summary>Answers a connection check without a database, and remembers what it was asked.</summary>
public sealed class FakeDatabaseConnectionCheck : IDatabaseConnectionCheck
{
    /// <summary>When set, the check fails with this.</summary>
    public Error? FailWith { get; set; }

    /// <summary>The details it was last asked to try, or null if it never was.</summary>
    public DatabaseSettings? Tried { get; private set; }

    public Task<Result> CheckAsync(DatabaseSettings settings, CancellationToken cancellationToken)
    {
        Tried = settings;

        return Task.FromResult(FailWith is { } failure ? Result.Failure(failure) : Result.Success());
    }
}
