using Application.Abstractions.Settings;
using Infrastructure.Identity;

namespace IntegrationTests.Identity;

/// <summary>
/// The per-account half of the sign-in limits, without a host: nothing here
/// needs a database or a pipeline.
/// </summary>
public class InMemoryLoginAttemptsTests
{
    private const int Budget = 5;
    private const string Account = "digest-of-ada";
    private const string Home = "198.51.100.7";
    private const string Attacker = "203.0.113.66";

    private static readonly DateTimeOffset Now = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TryReserve_ShouldAdmitOnlyTheBudget_WhenGuessesArriveAllAtOnce()
    {
        // Arrange
        var attempts = NewAttempts();
        var admitted = 0;

        // Act
        Parallel.For(0, 200, _ =>
        {
            if (attempts.TryReserve(Account, Attacker, Now))
            {
                Interlocked.Increment(ref admitted);
            }
        });

        // Assert
        // Checking first and counting only after a failure let every one of a
        // burst of parallel guesses through before the first was written down.
        Assert.Equal(Budget, admitted);
    }

    [Fact]
    public void TryReserve_ShouldRefuseEveryUnknownAddress_OnceTheAccountsBudgetIsSpent()
    {
        // Arrange
        var attempts = NewAttempts();
        Spend(attempts, Attacker);

        // Act
        var fromElsewhere = attempts.TryReserve(Account, "192.0.2.1", Now);

        // Assert
        // A botnet spreading one account's guesses over many addresses still
        // draws on one budget.
        Assert.False(fromElsewhere);
    }

    [Fact]
    public void TryReserve_ShouldStillAdmitTheOwner_FromAnAddressTheyHaveSignedInFrom()
    {
        // Arrange
        var attempts = NewAttempts();
        attempts.TryReserve(Account, Home, Now.AddDays(-3));
        attempts.Succeeded(Account, Home, Now.AddDays(-3));
        Spend(attempts, Attacker);

        // Act
        var fromHome = attempts.TryReserve(Account, Home, Now);

        // Assert
        // Five wrong guesses a minute from anywhere used to keep the owner out
        // for as long as the attacker cared to keep guessing.
        Assert.True(fromHome);
    }

    [Fact]
    public void TryReserve_ShouldStillBoundAKnownAddress_ByABudgetOfItsOwn()
    {
        // Arrange
        var attempts = NewAttempts();
        attempts.TryReserve(Account, Home, Now.AddDays(-3));
        attempts.Succeeded(Account, Home, Now.AddDays(-3));

        // Act
        Spend(attempts, Home);
        var oneMore = attempts.TryReserve(Account, Home, Now);

        // Assert
        Assert.False(oneMore);
    }

    [Fact]
    public void TryReserve_ShouldForgetAKnownAddress_OnceItHasNotSignedInForAMonth()
    {
        // Arrange
        var attempts = NewAttempts();
        attempts.TryReserve(Account, Home, Now.AddDays(-31));
        attempts.Succeeded(Account, Home, Now.AddDays(-31));
        Spend(attempts, Attacker);

        // Act
        var fromHome = attempts.TryReserve(Account, Home, Now);

        // Assert
        Assert.False(fromHome);
    }

    [Fact]
    public void TryReserve_ShouldAdmitAgain_WhenTheWindowHasPassed()
    {
        // Arrange
        var attempts = NewAttempts();
        Spend(attempts, Attacker);

        // Act
        var aMinuteLater = attempts.TryReserve(Account, Attacker, Now.AddMinutes(1));

        // Assert
        Assert.True(aMinuteLater);
    }

    [Fact]
    public void Succeeded_ShouldGiveTheBudgetBack_SoTypingErrorsDoNotAccumulate()
    {
        // Arrange
        var attempts = NewAttempts();

        for (var typo = 0; typo < Budget - 1; typo++)
        {
            attempts.TryReserve(Account, null, Now);
        }

        attempts.TryReserve(Account, null, Now);
        attempts.Succeeded(Account, null, Now);

        // Act
        var admitted = Enumerable.Range(0, Budget).Count(_ => attempts.TryReserve(Account, null, Now));

        // Assert
        Assert.Equal(Budget, admitted);
    }

    [Fact]
    public void TryReserve_ShouldLeaveOtherAccountsAlone_WhenOneIsLockedOut()
    {
        // Arrange
        var attempts = NewAttempts();
        Spend(attempts, Attacker);

        // Act
        var someoneElse = attempts.TryReserve("digest-of-grace", Attacker, Now);

        // Assert
        Assert.True(someoneElse);
    }

    private static InMemoryLoginAttempts NewAttempts() =>
        new(new RateLimitSettings { LoginPerAccountPerMinute = Budget });

    private static void Spend(InMemoryLoginAttempts attempts, string clientAddress)
    {
        for (var guess = 0; guess < Budget; guess++)
        {
            Assert.True(attempts.TryReserve(Account, clientAddress, Now));
        }
    }
}
