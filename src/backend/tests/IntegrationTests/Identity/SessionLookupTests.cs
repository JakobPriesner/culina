using System.Net;
using Application.Abstractions;
using Domain.Sessions;
using Domain.Shared;
using Infrastructure.Identity;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Identity;

/// <summary>What an authenticated request costs the sessions table, proven through the pipeline.</summary>
[Collection(RequiresDatabase.Name)]
public class SessionLookupTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task AnUnsafeRequest_ShouldReadItsSessionOnce()
    {
        await postgres.ResetAsync(Token);

        var lookups = new LookupCounter();
        using var factory = new CulinaApiFactory(
            postgres,
            replace: services => services.AddScoped<ISessionStore>(provider =>
                new CountingStore(ActivatorUtilities.CreateInstance<SessionStore>(provider), lookups)));
        using var client = factory.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);
        await client.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);
        lookups.Reset();

        var response = await client.PostAsync("/api/v1/households", new { name = "Cabin" }, Token);

        // Authentication reads the session; the CSRF guard reuses that read.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, lookups.Count);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class LookupCounter
    {
        private int count;

        internal int Count => Volatile.Read(ref count);

        internal void Add() => Interlocked.Increment(ref count);

        internal void Reset() => Volatile.Write(ref count, 0);
    }

    private sealed class CountingStore(ISessionStore inner, LookupCounter lookups) : ISessionStore
    {
        public Task<Result<Session>> FindActiveByTokenAsync(
            string token,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            lookups.Add();

            return inner.FindActiveByTokenAsync(token, now, cancellationToken);
        }

        public Task<IReadOnlyList<Session>> ForUserAsync(Guid userId, CancellationToken cancellationToken) =>
            inner.ForUserAsync(userId, cancellationToken);

        public Task<Result> AddAsync(Session session, CancellationToken cancellationToken) =>
            inner.AddAsync(session, cancellationToken);

        public Task<Result> RenewAsync(Session session, CancellationToken cancellationToken) =>
            inner.RenewAsync(session, cancellationToken);

        public Task<Result> RevokeAsync(Guid sessionId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
            inner.RevokeAsync(sessionId, userId, now, cancellationToken);

        public Task RevokeAllAsync(Guid userId, Guid? keepSessionId, DateTimeOffset now, CancellationToken cancellationToken) =>
            inner.RevokeAllAsync(userId, keepSessionId, now, cancellationToken);

        public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
            inner.DeleteExpiredAsync(now, cancellationToken);
    }
}
