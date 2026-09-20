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
        world.Settings.Uses = [Use(Capability.Draft, AssistantKind.OpenAi, on: false)];

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
    public async Task ComposeStreamAsync_ShouldRefuseBeforeTheStreamOpens_WhenTheBudgetIsSpent()
    {
        // Arrange
        var world = new World();
        world.Ledger.RefuseWith = AssistanceErrors.BudgetExhausted;

        // Act
        var result = await world.ComposeStreamAsync();

        // Assert
        // The reason this returns a result wrapping a stream rather than a
        // stream that can fail. Once the first event is out the response is a
        // 200 that has begun, and nothing after that can be a 429 — so every
        // check that decides whether the call may happen runs first.
        result.ShouldBeFailure(AssistanceErrors.BudgetExhausted);
        Assert.Equal(0, world.Assistant.Calls);
    }

    [Fact]
    public async Task ComposeStreamAsync_ShouldSettleWithWhatWasUsed_WhenItRanToTheEnd()
    {
        // Arrange
        var world = new World();
        world.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" }, new ModelUsage(120, 340, 0));

        // Act
        var parts = await world.ReadToTheEndAsync();

        // Assert
        // Thin first, whole last: that is what the screen shows arriving.
        Assert.Equal(2, parts.Count);
        Assert.False(parts[0].Finished);
        Assert.True(parts[^1].Finished);

        var settlement = Assert.Single(world.Ledger.Settled);
        Assert.Equal("ok", settlement.Outcome);
        Assert.Equal(340, settlement.Usage.OutputTokens);
    }

    [Fact]
    public async Task ComposeStreamAsync_ShouldStillSettle_WhenTheProviderStoppedPartWay()
    {
        // Arrange
        var world = new World();
        world.Assistant.WillCompose(Result<Composed>.Failure(AssistanceErrors.Throttled));

        // Act
        var parts = await world.ReadToTheEndAsync();

        // Assert
        Assert.Equal(AssistanceErrors.Throttled, Assert.Single(parts).Failure);
        Assert.Equal("assistance.throttled", Assert.Single(world.Ledger.Settled).Outcome);
    }

    [Fact]
    public async Task ComposeStreamAsync_ShouldNotTellACookThatTheKeyWasRefused()
    {
        // Arrange
        var world = new World();
        world.Assistant.WillCompose(Result<Composed>.Failure(AssistanceErrors.Rejected));

        // Act
        var parts = await world.ReadToTheEndAsync();

        // Assert
        // A refused key is the administrator's to fix and is nothing somebody
        // halfway through a recipe can act on. The ledger keeps the real code.
        Assert.Equal(AssistanceErrors.Unavailable, Assert.Single(parts).Failure);
        Assert.Equal("assistance.rejected", Assert.Single(world.Ledger.Settled).Outcome);
    }

    [Fact]
    public async Task ComposeStreamAsync_ShouldSettle_WhenNobodyReadsToTheEnd()
    {
        // Arrange
        var world = new World();
        world.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" }, new ModelUsage(120, 340, 0));

        var opened = (await world.ComposeStreamAsync()).ShouldBeSuccess();

        // Act
        // One part, and then the reader walks away — which is what a person
        // closing the page looks like from here, and is an ordinary end rather
        // than an edge case.
        await using (var parts = opened.GetAsyncEnumerator(Token))
        {
            Assert.True(await parts.MoveNextAsync());
        }

        // Assert
        // A reservation nobody settled holds its estimate against the month's
        // budget until the month turns.
        var settlement = Assert.Single(world.Ledger.Settled);
        Assert.Equal("abandoned", settlement.Outcome);
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
        world.Settings.Uses = [Use(Capability.Draw, AssistantKind.Ollama)];

        // Act
        var result = await world.DrawAsync();

        // Assert
        // Refused by the settings rather than by the adapter: Ollama cannot
        // draw, so the capability is not allowed however the switch is left.
        result.ShouldBeFailure(AssistanceErrors.Disabled);
        Assert.Equal(0, world.Assistant.Calls);
    }

    [Fact]
    public async Task ComposeAsync_ShouldCallTheProviderThisJobWasPointedAt()
    {
        // Arrange
        // Two connected, and the job points at the second.
        var world = new World();
        world.Settings.Connections =
        [
            new AssistanceConnection { Provider = "openai", ProtectedApiKey = "protected:one" },
            new AssistanceConnection { Provider = "gemini", ProtectedApiKey = "protected:two" }
        ];
        world.Settings.Uses = [Use(Capability.Draft, AssistantKind.Gemini)];
        world.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" });

        // Act
        await world.ComposeAsync();

        // Assert
        // The reason for any of this: each job reaches the provider it was
        // pointed at rather than whichever one happened to be first.
        Assert.Equal(AssistantKind.Gemini, Assert.Single(world.Ledger.Reservations).Provider);
        Assert.Equal("two", world.Assistant.LastConnection!.ApiKey);
    }

    [Fact]
    public async Task ComposeAsync_ShouldUseTheChosenModel_AndTheDefaultWhenNoneWasChosen()
    {
        // Arrange
        var world = new World();
        world.Settings.Uses = [Use(Capability.Draft, AssistantKind.OpenAi, model: "cheap-one")];
        world.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" });

        // Act
        await world.ComposeAsync();

        // Assert
        Assert.Equal("cheap-one", world.Assistant.LastConnection!.Model);

        // Arrange again, with nothing chosen.
        var second = new World();
        second.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" });

        // Act
        await second.ComposeAsync();

        // Assert
        // Empty means "whatever is current for this job", which the registry
        // answers — not a name this build was shipped believing.
        Assert.Equal("openai-default-draft", second.Assistant.LastConnection!.Model);
    }

    [Fact]
    public async Task ComposeAsync_ShouldUseTheProvidersOwnAddress_WhenNobodyOverrodeIt()
    {
        // Arrange
        var world = new World();
        world.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" });

        // Act
        await world.ComposeAsync();

        // Assert
        Assert.Equal("https://openai.example.com", world.Assistant.LastConnection!.BaseUrl);
    }

    [Fact]
    public async Task ComposeAsync_ShouldRefuse_WhenTheKeyRingCannotReadTheStoredKey()
    {
        // Arrange
        var world = new World();
        world.Protector.KeysLost = true;

        // Act
        var result = await world.ComposeAsync();

        // Assert
        // A key ring lost and restored empty leaves intact ciphertext nobody
        // can read. That is "no assistant is configured", not a crash.
        result.ShouldBeFailure(AssistanceErrors.NotConfigured);
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

    private static CapabilityUse Use(
        Capability capability,
        AssistantKind provider,
        bool on = true,
        string model = "") =>
        new()
        {
            Capability = capability.Code,
            Enabled = on,
            Provider = provider.Code,
            Model = model
        };

    /// <summary>The run, and the things it talks to.</summary>
    private sealed class World
    {
        internal World(bool connected = true, AssistantKind? provider = null)
        {
            var kind = provider ?? AssistantKind.OpenAi;

            Settings = new AssistanceSettings
            {
                Enabled = connected,
                Connections = connected
                    ?
                    [
                        new AssistanceConnection
                        {
                            Provider = kind.Code,
                            ProtectedApiKey = kind.NeedsApiKey ? "protected:secret" : string.Empty,
                            BaseUrl = kind.NeedsAddress ? "http://localhost:11434" : string.Empty
                        }
                    ]
                    : [],
                Uses = [Use(Capability.Draft, kind), Use(Capability.Draw, kind)]
            };

            Assistant = new FakeAssistant(kind);
        }

        internal AssistanceSettings Settings { get; }

        internal FakeAssistant Assistant { get; }

        internal FakeAssistanceLedger Ledger { get; } = new();

        internal FakeSecretProtector Protector { get; } = new();

        private AssistantRun Run => new(
            Settings,
            new FakeAssistants(Assistant),
            Protector,
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

        internal Task<Result<IAsyncEnumerable<Composing>>> ComposeStreamAsync() =>
            Run.ComposeStreamAsync(
                new Asker(Guid.CreateVersion7(), Guid.CreateVersion7()),
                new Composition
                {
                    Capability = Capability.Draft,
                    Language = Language.En,
                    Instruction = "Write a recipe.",
                    Material = "something with aubergines"
                },
                Token);

        /// <summary>Every part of a stream that was read properly.</summary>
        internal async Task<IReadOnlyList<Composing>> ReadToTheEndAsync()
        {
            var opened = (await ComposeStreamAsync()).ShouldBeSuccess();

            List<Composing> parts = [];

            await foreach (var part in opened.WithCancellation(Token))
            {
                parts.Add(part);
            }

            return parts;
        }

        internal Task<Result<Drawn>> DrawAsync() =>
            Run.DrawAsync(
                new Asker(Guid.CreateVersion7(), Guid.CreateVersion7()),
                new Drawing { Subject = "a bowl of soup" },
                Token);
    }
}
