using Application.Abstractions;
using Application.Abstractions.Settings;
using Application.Assistance;
using Domain.Assistance;
using Domain.Shared;
using TestSupport;

namespace Application.UnitTests.Assistance;

/// <summary>
/// The gate every assisted call goes through: allowed, afforded, and counted
/// however it went.
/// </summary>
public class AssistantRunTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ComposeAsync_ShouldNotCallTheModel_WhenNoAssistantIsConnected()
    {
        // Arrange
        var world = new World(connected: false);

        // Act
        var result = await world.ComposeAsync();

        // Assert
        // Not configured rather than forbidden: an instance with no assistant
        // is one where the thing does not exist.
        result.ShouldBeFailure(AssistanceErrors.NotConfigured);
        Assert.Equal(0, world.Assistant.Calls);
        Assert.Empty(world.Ledger.Reservations);
    }

    [Fact]
    public async Task ComposeAsync_ShouldNotCallTheModel_WhenThatCapabilityIsSwitchedOff()
    {
        // Arrange
        var world = new World();
        world.Settings.DraftEnabled = false;

        // Act
        var result = await world.ComposeAsync();

        // Assert
        result.ShouldBeFailure(AssistanceErrors.Disabled);
        Assert.Equal(0, world.Assistant.Calls);
    }

    [Fact]
    public async Task ComposeAsync_ShouldNotCallTheModel_WhenTheBudgetIsSpent()
    {
        // Arrange
        var world = new World();
        world.Ledger.RefuseWith = AssistanceErrors.BudgetExhausted;

        // Act
        var result = await world.ComposeAsync();

        // Assert
        // The whole point of reserving before calling: the money is checked
        // before it can be spent, not after.
        result.ShouldBeFailure(AssistanceErrors.BudgetExhausted);
        Assert.Equal(0, world.Assistant.Calls);
    }

    [Fact]
    public async Task ComposeAsync_ShouldSettleWithWhatWasUsed_WhenItWorked()
    {
        // Arrange
        var world = new World();
        world.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" }, new ModelUsage(120, 340, 0));

        // Act
        var draft = (await world.ComposeAsync()).ShouldBeSuccess();

        // Assert
        Assert.Equal("Soup", draft.Title);

        var settlement = Assert.Single(world.Ledger.Settled);
        Assert.Equal("ok", settlement.Outcome);
        Assert.Equal(120, settlement.Usage.InputTokens);
        Assert.Equal(340, settlement.Usage.OutputTokens);
    }

    [Fact]
    public async Task ComposeAsync_ShouldStillSettle_WhenTheProviderFailed()
    {
        // Arrange
        var world = new World();
        world.Assistant.WillCompose(Result<Composed>.Failure(AssistanceErrors.Throttled));

        // Act
        var result = await world.ComposeAsync();

        // Assert
        // The one that is easy to get wrong. A reservation nobody settled holds
        // its estimate against the month's budget until the month turns, so a
        // provider having a bad afternoon would quietly spend the ceiling.
        result.ShouldBeFailure(AssistanceErrors.Throttled);

        var settlement = Assert.Single(world.Ledger.Settled);
        Assert.Equal("assistance.throttled", settlement.Outcome);
    }

    [Fact]
    public async Task ComposeAsync_ShouldReserveAgainstTheConfiguredCeilings()
    {
        // Arrange
        var world = new World();
        world.Settings.MonthlyBudget = 20m;
        world.Settings.PersonalBudget = 5m;
        world.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" });

        // Act
        await world.ComposeAsync();

        // Assert
        var reservation = Assert.Single(world.Ledger.Reservations);
        Assert.Equal(20m, reservation.MonthlyBudget);
        Assert.Equal(5m, reservation.PersonalBudget);
        Assert.Equal(Capability.Draft, reservation.Capability);
    }

    [Fact]
    public async Task DrawAsync_ShouldBeRefused_ForAProviderThatCannotDraw()
    {
        // Arrange
        var world = new World(provider: AssistantKind.Ollama);
        world.Settings.BaseUrl = "http://localhost:11434";
        world.Settings.DrawEnabled = true;

        // Act
        var result = await world.DrawAsync();

        // Assert
        // Refused by the settings rather than by the adapter: Ollama cannot
        // draw, so the capability is not allowed however the switch is left.
        result.ShouldBeFailure(AssistanceErrors.Disabled);
        Assert.Equal(0, world.Assistant.Calls);
    }

    [Fact]
    public void StartOfMonth_ShouldBeTheFirstInstantInUtc_BecauseThatIsHowAProviderBills()
    {
        // Act
        var start = AssistantRun.StartOfMonth(new DateTimeOffset(2026, 9, 19, 14, 30, 0, TimeSpan.FromHours(2)));

        // Assert
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), start);
    }

    /// <summary>The run, and the three things it talks to.</summary>
    private sealed class World
    {
        internal World(bool connected = true, AssistantKind? provider = null)
        {
            var kind = provider ?? AssistantKind.OpenAi;

            Settings = new AssistanceSettings
            {
                Enabled = connected,
                Provider = kind.Code,
                ProtectedApiKey = connected && kind.NeedsApiKey ? "protected" : string.Empty,
                ComposeModel = "gpt-4o-mini",
                DrawModel = "gpt-image-1"
            };

            Assistant = new FakeAssistant(kind);
        }

        internal AssistanceSettings Settings { get; }

        internal FakeAssistant Assistant { get; }

        internal FakeAssistanceLedger Ledger { get; } = new();

        private AssistantRun Run => new(
            Settings,
            new FakeAssistants(Assistant),
            Ledger,
            new FakeModelPrices(),
            TimeProvider.System);

        internal Task<Result<DraftedRecipe>> ComposeAsync() =>
            Run.ComposeAsync(
                new Asker(Guid.CreateVersion7(), Guid.CreateVersion7()),
                new Composition
                {
                    Capability = Capability.Draft,
                    Language = Language.En,
                    Instruction = "Write a recipe.",
                    Material = "something with aubergines"
                },
                Token);

        internal Task<Result<Drawn>> DrawAsync() =>
            Run.DrawAsync(
                new Asker(Guid.CreateVersion7(), Guid.CreateVersion7()),
                new Drawing { Subject = "a bowl of soup" },
                Token);
    }
}
