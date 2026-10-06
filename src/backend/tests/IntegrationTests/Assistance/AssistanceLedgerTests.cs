using Application.Abstractions;
using Domain.Assistance;
using Domain.Shared;
using Domain.Users;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Assistance;
using Infrastructure.Persistence.Users;
using IntegrationTests.Fixtures;
using TestSupport;

namespace IntegrationTests.Assistance;

/// <summary>
/// The budget gate, against a real database and several requests at once.
/// </summary>
/// <remarks>
/// Only a real database can show this. The gate used to be one insert that
/// checked the total in its own WHERE, and under read committed every one of a
/// burst of simultaneous requests read the same total, found room and inserted.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class AssistanceLedgerTests(PostgresFixture postgres)
{
    /// <summary>Long enough ago that every row written now counts against the budget.</summary>
    private static readonly DateTimeOffset Since = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task ReserveAsync_ShouldLetOnlyWhatFits_WhenTenRequestsArriveAtOnce()
    {
        // Arrange
        var userId = await SeedUserAsync();

        // Room for three reservations of ten cents, and not a fourth.
        var reservation = Reservation(userId, monthlyBudget: 0.30m, personalBudget: null);

        // Act
        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => Task.Run(() => ReserveAsync(reservation), Token)));

        // Assert
        Assert.Equal(3, await RowsAsync());
        Assert.Equal(3, results.Count(result => result.Match(_ => true, _ => false)));
        Assert.All(
            results.Where(result => result.Match(_ => false, _ => true)),
            result => result.ShouldBeFailure(AssistanceErrors.BudgetExhausted));
    }

    [Fact]
    public async Task ReserveAsync_ShouldHoldOnePersonToTheirShare_WhenTheirRequestsArriveAtOnce()
    {
        // Arrange
        var userId = await SeedUserAsync();
        var reservation = Reservation(userId, monthlyBudget: null, personalBudget: 0.30m);

        // Act
        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => Task.Run(() => ReserveAsync(reservation), Token)));

        // Assert
        Assert.Equal(3, await RowsAsync());
        Assert.All(
            results.Where(result => result.Match(_ => false, _ => true)),
            result => result.ShouldBeFailure(AssistanceErrors.PersonalBudgetExhausted));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Reservation Reservation(Guid userId, decimal? monthlyBudget, decimal? personalBudget) => new(
        userId,
        HouseholdId: null,
        Capability.Draft,
        AssistantKind.OpenAi,
        "gpt-4o",
        Estimate: 0.10m,
        monthlyBudget,
        personalBudget,
        Since);

    /// <summary>One request's own connection, the way each request has one.</summary>
    private async Task<Result<Guid>> ReserveAsync(Reservation reservation)
    {
        await using var session = postgres.NewSession();

        var ledger = new AssistanceLedger(new DbExecutor(session), new UnitOfWork(session), TimeProvider.System);

        return await ledger.ReserveAsync(reservation, Token);
    }

    private async Task<Guid> SeedUserAsync()
    {
        _ = postgres.Api.Services;
        await postgres.ResetAsync(Token);

        await using var session = postgres.NewSession();

        var user = User.Register(
            Email.Create("ada@example.com").ShouldBeSuccess(),
            DisplayName.Create("Ada").ShouldBeSuccess(),
            "argon2id$hash",
            TimeProvider.System.GetUtcNow());

        (await new UserRepository(new DbExecutor(session)).AddAsync(user, false, Token)).ShouldBeSuccess();

        return user.Id;
    }

    private Task<long> RowsAsync() =>
        postgres.QuerySingleAsync<long>("select count(*) from assistance_usage;", Token);
}
