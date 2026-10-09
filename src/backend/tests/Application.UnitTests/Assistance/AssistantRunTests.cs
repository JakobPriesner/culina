using Application.Abstractions;
using Application.Abstractions.Settings;
using Application.Assistance;
using Domain.Assistance;
using Domain.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using TestSupport;

namespace Application.UnitTests.Assistance;

/// <summary>
/// The gate every assisted call goes through: allowed, afforded, and counted however it went.
/// </summary>
public class AssistantRunTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ComposeAsync_ShouldNotCallTheModel_WhenNoAssistantIsConnected()
    {
        var world = new World(connected: false);

        var result = await world.ComposeAsync();

        // Not configured rather than forbidden: with no assistant the thing does not exist.
        result.ShouldBeFailure(AssistanceErrors.NotConfigured);
        Assert.Equal(0, world.Assistant.Calls);
        Assert.Empty(world.Ledger.Reservations);
    }

    [Fact]
    public async Task ComposeAsync_ShouldNotCallTheModel_WhenThatCapabilityIsSwitchedOff()
    {
        var world = new World();
        world.Settings.Uses = [Use(Capability.Draft, AssistantKind.OpenAi, on: false)];

        var result = await world.ComposeAsync();

        result.ShouldBeFailure(AssistanceErrors.Disabled);
        Assert.Equal(0, world.Assistant.Calls);
    }

    [Fact]
    public async Task ComposeAsync_ShouldNotCallTheModel_WhenTheBudgetIsSpent()
    {
        var world = new World();
        world.Ledger.RefuseWith = AssistanceErrors.BudgetExhausted;

        var result = await world.ComposeAsync();

        // Reserving before calling is the point: the money is checked before it can be spent.
        result.ShouldBeFailure(AssistanceErrors.BudgetExhausted);
        Assert.Equal(0, world.Assistant.Calls);
    }

    [Fact]
    public async Task ComposeAsync_ShouldSettleWithWhatWasUsed_WhenItWorked()
    {
        var world = new World();
        world.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" }, new ModelUsage(120, 340, 0));

        var draft = (await world.ComposeAsync()).ShouldBeSuccess();

        Assert.Equal("Soup", draft.Title);

        var settlement = Assert.Single(world.Ledger.Settled);
        Assert.Equal("ok", settlement.Outcome);
        Assert.Equal(120, settlement.Usage.InputTokens);
        Assert.Equal(340, settlement.Usage.OutputTokens);
    }

    [Fact]
    public async Task ComposeAsync_ShouldStillSettle_WhenTheProviderFailed()
    {
        var world = new World();
        world.Assistant.WillCompose(Result<Composed>.Failure(AssistanceErrors.Throttled));

        var result = await world.ComposeAsync();

        // Easy to get wrong: an unsettled reservation holds its estimate against the month's
        // budget, so a provider having a bad afternoon would quietly spend the ceiling.
        result.ShouldBeFailure(AssistanceErrors.Throttled);

        var settlement = Assert.Single(world.Ledger.Settled);
        Assert.Equal("assistance.throttled", settlement.Outcome);
    }

    [Fact]
    public async Task ComposeAsync_ShouldSettleWithNoCost_WhenItFailedWithoutSayingWhatItUsed()
    {
        var world = new World();
        world.Assistant.WillCompose(Result<Composed>.Failure(AssistanceErrors.UnusableAnswer));

        await world.ComposeAsync();

        // Null, not a priced zero: a zero would stop the reservation's estimate counting for a call
        // the provider may well have billed.
        Assert.Null(Assert.Single(world.Ledger.Settled).Cost);
    }

    [Fact]
    public async Task ComposeAsync_ShouldSettlePriced_WhenTheProviderRefusedBeforeGenerating()
    {
        var world = new World();
        world.Assistant.WillCompose(Result<Composed>.Failure(AssistanceErrors.Throttled));

        await world.ComposeAsync();

        // Nothing was generated, so holding the estimate would let a burst of 429s lock the budget.
        Assert.Equal(0.01m, Assert.Single(world.Ledger.Settled).Cost);
    }

    [Fact]
    public async Task ComposeAsync_ShouldSettleAtZero_WhenALocalModelFailed()
    {
        var world = new World(provider: AssistantKind.Ollama);
        world.Assistant.WillCompose(Result<Composed>.Failure(AssistanceErrors.UnusableAnswer));

        await world.ComposeAsync();

        Assert.Equal(0.01m, Assert.Single(world.Ledger.Settled).Cost);
    }

    [Fact]
    public async Task ComposeStreamAsync_ShouldRefuseBeforeTheStreamOpens_WhenTheBudgetIsSpent()
    {
        var world = new World();
        world.Ledger.RefuseWith = AssistanceErrors.BudgetExhausted;

        var result = await world.ComposeStreamAsync();

        // A result wrapping a stream, not a stream that can fail: once the first event is out the
        // response is a started 200 and nothing after can be a 429, so every check runs first.
        result.ShouldBeFailure(AssistanceErrors.BudgetExhausted);
        Assert.Equal(0, world.Assistant.Calls);
    }

    [Fact]
    public async Task ComposeStreamAsync_ShouldSettleWithWhatWasUsed_WhenItRanToTheEnd()
    {
        var world = new World();
        world.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" }, new ModelUsage(120, 340, 0));

        var parts = await world.ReadToTheEndAsync();

        // Thin first, whole last: what the screen shows arriving.
        Assert.Equal(2, parts.Count);
        Assert.False(parts[0].Finished);
        Assert.True(parts[^1].Finished);

        var settlement = Assert.Single(world.Ledger.Settled);
        Assert.Equal("ok", settlement.Outcome);
        Assert.Equal(340, settlement.Usage.OutputTokens);
        Assert.NotNull(settlement.Cost);
    }

    [Fact]
    public async Task ComposeStreamAsync_ShouldStillSettle_WhenTheProviderStoppedPartWay()
    {
        var world = new World();
        world.Assistant.WillCompose(Result<Composed>.Failure(AssistanceErrors.Throttled));

        var parts = await world.ReadToTheEndAsync();

        Assert.Equal(AssistanceErrors.Throttled, Assert.Single(parts).Failure);

        var settlement = Assert.Single(world.Ledger.Settled);
        Assert.Equal("assistance.throttled", settlement.Outcome);
        Assert.Equal(0.01m, settlement.Cost);
    }

    [Fact]
    public async Task ComposeStreamAsync_ShouldNotTellACookThatTheKeyWasRefused()
    {
        var world = new World();
        world.Assistant.WillCompose(Result<Composed>.Failure(AssistanceErrors.Rejected));

        var parts = await world.ReadToTheEndAsync();

        // A refused key is the administrator's to fix, not something a person mid-recipe can act
        // on; the ledger keeps the real code.
        Assert.Equal(AssistanceErrors.Unavailable, Assert.Single(parts).Failure);
        Assert.Equal("assistance.rejected", Assert.Single(world.Ledger.Settled).Outcome);
    }

    [Fact]
    public async Task ComposeStreamAsync_ShouldTellACookThatTheModelIsMissing()
    {
        var world = new World();
        world.Assistant.WillCompose(Result<Composed>.Failure(AssistanceErrors.ModelMissing));

        var parts = await world.ReadToTheEndAsync();

        // Not folded into "unavailable": waiting never makes a model appear, and the cook is often
        // the person who can pull it.
        Assert.Equal(AssistanceErrors.ModelMissing, Assert.Single(parts).Failure);
        Assert.Equal("assistance.model_missing", Assert.Single(world.Ledger.Settled).Outcome);
    }

    [Fact]
    public async Task ComposeStreamAsync_ShouldSettle_WhenNobodyReadsToTheEnd()
    {
        var world = new World();
        world.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" }, new ModelUsage(120, 340, 0));

        var opened = (await world.ComposeStreamAsync()).ShouldBeSuccess();

        // One part, then the reader walks away as a person closing the page does: an ordinary end,
        // not an edge case.
        await using (var parts = opened.GetAsyncEnumerator(Token))
        {
            Assert.True(await parts.MoveNextAsync());
        }

        // An unsettled reservation holds its estimate against the month's budget until the month
        // turns.
        var settlement = Assert.Single(world.Ledger.Settled);
        Assert.Equal("abandoned", settlement.Outcome);

        // Usage arrives with the last part, which was never read: unknown, not free.
        Assert.Null(settlement.Cost);
    }

    [Fact]
    public async Task ComposeAsync_ShouldReserveAgainstTheConfiguredCeilings()
    {
        var world = new World();
        world.Settings.MonthlyBudget = 20m;
        world.Settings.PersonalBudget = 5m;
        world.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" });

        await world.ComposeAsync();

        var reservation = Assert.Single(world.Ledger.Reservations);
        Assert.Equal(20m, reservation.MonthlyBudget);
        Assert.Equal(5m, reservation.PersonalBudget);
        Assert.Equal(Capability.Draft, reservation.Capability);
    }

    [Fact]
    public async Task DrawAsync_ShouldBeRefused_ForAProviderThatCannotDraw()
    {
        var world = new World(provider: AssistantKind.Ollama);
        world.Settings.Uses = [Use(Capability.Draw, AssistantKind.Ollama)];

        var result = await world.DrawAsync();

        // Refused by the settings, not the adapter: Ollama cannot draw, however the switch is left.
        result.ShouldBeFailure(AssistanceErrors.Disabled);
        Assert.Equal(0, world.Assistant.Calls);
    }

    [Fact]
    public async Task ComposeAsync_ShouldCallTheProviderThisJobWasPointedAt()
    {
        // Two connected, and the job points at the second.
        var world = new World();
        world.Settings.Connections =
        [
            new AssistanceConnection { Provider = "openai", ProtectedApiKey = "protected:one" },
            new AssistanceConnection { Provider = "gemini", ProtectedApiKey = "protected:two" }
        ];
        world.Settings.Uses = [Use(Capability.Draft, AssistantKind.Gemini)];
        world.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" });

        await world.ComposeAsync();

        // Each job reaches the provider it was pointed at, not whichever was first.
        Assert.Equal(AssistantKind.Gemini, Assert.Single(world.Ledger.Reservations).Provider);
        Assert.Equal("two", world.Assistant.LastConnection!.ApiKey);
    }

    [Fact]
    public async Task ComposeAsync_ShouldUseTheChosenModel_AndTheDefaultWhenNoneWasChosen()
    {
        var world = new World();
        world.Settings.Uses = [Use(Capability.Draft, AssistantKind.OpenAi, model: "cheap-one")];
        world.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" });

        await world.ComposeAsync();

        Assert.Equal("cheap-one", world.Assistant.LastConnection!.Model);

        var second = new World();
        second.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" });

        await second.ComposeAsync();

        // Empty means "whatever is current for this job", answered by the registry, not a name this
        // build shipped believing.
        Assert.Equal("openai-default-draft", second.Assistant.LastConnection!.Model);
    }

    [Fact]
    public async Task ComposeAsync_ShouldUseTheProvidersOwnAddress_WhenNobodyOverrodeIt()
    {
        var world = new World();
        world.Assistant.WillCompose(new DraftedRecipe { Title = "Soup" });

        await world.ComposeAsync();

        Assert.Equal("https://openai.example.com", world.Assistant.LastConnection!.BaseUrl);
    }

    [Fact]
    public async Task ComposeAsync_ShouldRefuse_WhenTheKeyRingCannotReadTheStoredKey()
    {
        var world = new World();
        world.Protector.KeysLost = true;

        var result = await world.ComposeAsync();

        // A key ring lost and restored empty leaves unreadable ciphertext: "no assistant
        // configured", not a crash.
        result.ShouldBeFailure(AssistanceErrors.NotConfigured);
        Assert.Equal(0, world.Assistant.Calls);
    }

    [Fact]
    public void StartOfMonth_ShouldBeTheFirstInstantInUtc_BecauseThatIsHowAProviderBills()
    {
        var start = AssistantRun.StartOfMonth(new DateTimeOffset(2026, 9, 19, 14, 30, 0, TimeSpan.FromHours(2)));

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
            TimeProvider.System,
            NullLogger<AssistantRun>.Instance);

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
