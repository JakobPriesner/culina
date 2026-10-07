using Application.Abstractions.Settings;
using Application.Settings.UpdateAssistance;
using Domain.Assistance;
using Domain.Shared;
using TestSupport;

namespace Application.UnitTests.Settings;

/// <summary>Connecting several providers at once, and pointing each job at one.</summary>
public class UpdateAssistanceSettingsCommandHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ShouldConnectSeveralProvidersAtOnce()
    {
        var world = new World();

        var response = (await world.Handle(Command(
                connections: [Hosted("openai", "sk-one"), Hosted("gemini", "sk-two"), Local()],
                uses: [Use("improve", "openai"), Use("draw", "gemini"), Use("read", "ollama")])))
            .ShouldBeSuccess();

        // Providers are not interchangeable: each job gets the one it is good at.
        Assert.All(response.Connections, connection => Assert.True(connection.Usable));
        Assert.Equal(3, world.Settings.Connections.Count);
    }

    [Fact]
    public async Task Handle_ShouldSendEachJobToTheProviderItWasPointedAt()
    {
        var world = new World();

        await world.Handle(Command(
            connections: [Hosted("openai", "sk-one"), Hosted("gemini", "sk-two")],
            uses: [Use("improve", "openai", "cheap-one"), Use("draw", "gemini")]));

        Assert.Equal("openai", world.Settings.UseFor(Capability.Improve)!.Provider);
        Assert.Equal("cheap-one", world.Settings.UseFor(Capability.Improve)!.Model);
        Assert.Equal("gemini", world.Settings.UseFor(Capability.Draw)!.Provider);
    }

    [Fact]
    public async Task Handle_ShouldProtectEveryKey_RatherThanStoringWhatWasTyped()
    {
        var world = new World();

        await world.Handle(Command(
            connections: [Hosted("openai", "sk-one"), Hosted("gemini", "sk-two")],
            uses: []));

        Assert.All(
            world.Settings.Connections,
            one => Assert.StartsWith("protected:", one.ProtectedApiKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_ShouldKeepEachStoredKey_WhenTheFormLeavesItOut()
    {
        var world = new World();
        await world.Handle(Command(
            connections: [Hosted("openai", "sk-one"), Hosted("gemini", "sk-two")],
            uses: []));

        // The same form saved again to change a budget, with no keys in it.
        await world.Handle(Command(
            connections: [Hosted("openai", apiKey: null), Hosted("gemini", apiKey: null)],
            uses: [],
            monthlyBudget: 25m));

        Assert.Equal(2, world.Settings.Connections.Count);
        Assert.All(world.Settings.Connections, one => Assert.True(one.HasApiKey));
        Assert.Equal(25m, world.Settings.MonthlyBudget);
    }

    [Theory]
    [InlineData("https://collector.example/v1")]
    [InlineData("")]
    public async Task Handle_ShouldNotKeepAStoredKey_WhenTheAddressChanges(string address)
    {
        var world = new World();
        await world.Handle(Command(
            connections: [new ConnectionEdit("openai", "sk-one", "https://proxy.example/v1")],
            uses: []));

        var result = await world.Handle(Command(
            connections: [new ConnectionEdit("openai", ApiKey: null, address)],
            uses: []));

        // The key would go wherever the new address says.
        result.ShouldBeFailure(AssistanceErrors.ApiKeyRequired);
        Assert.Equal("https://proxy.example/v1", world.Settings.Connections.Single().BaseUrl);
    }

    [Fact]
    public async Task Handle_ShouldMoveAProviderToANewAddress_WhenItsKeyIsEnteredAgain()
    {
        var world = new World();
        await world.Handle(Command(connections: [Hosted("openai", "sk-one")], uses: []));

        var result = await world.Handle(Command(
            connections: [new ConnectionEdit("openai", "sk-one", "https://proxy.example/v1")],
            uses: []));

        result.ShouldBeSuccess();
        Assert.Equal("https://proxy.example/v1", world.Settings.Connections.Single().BaseUrl);
    }

    [Fact]
    public async Task Handle_ShouldDisconnectAProvider_WhenItsKeyIsCleared()
    {
        var world = new World();
        await world.Handle(Command(
            connections: [Hosted("openai", "sk-one"), Hosted("gemini", "sk-two")],
            uses: []));

        await world.Handle(Command(
            connections: [Hosted("openai", apiKey: ""), Hosted("gemini", apiKey: null)],
            uses: []));

        // An empty string is somebody clearing the box (unlike not sending it); a connection with nothing in it is not stored.
        Assert.Single(world.Settings.Connections);
        Assert.Equal("gemini", world.Settings.Connections[0].Provider);
    }

    [Fact]
    public async Task Handle_ShouldConnectOllamaWithNoKeyAtAll_GivenAnAddress()
    {
        var world = new World();

        var response = (await world.Handle(Command(connections: [Local()], uses: [])))
            .ShouldBeSuccess();

        // Breaks "connected means has a key": a local model has nobody to authenticate to.
        var ollama = response.Connections.Single(one => one.Provider == "ollama");
        Assert.True(ollama.Usable);
        Assert.False(ollama.ApiKeyConfigured);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_PointingDrawingAtAProviderThatCannotDraw()
    {
        var world = new World();

        var result = await world.Handle(Command(
            connections: [Local()],
            uses: [Use("draw", "ollama")]));

        // Caught at the form so nobody switches on a capability that could never work.
        result.ShouldBeFailure(AssistanceErrors.DrawingNotSupported);
    }

    [Fact]
    public async Task Handle_ShouldAcceptAJobPointedAtAProviderNobodyHasConnectedYet()
    {
        var world = new World();

        var result = await world.Handle(Command(
            connections: [Hosted("openai", "sk-one")],
            uses: [Use("improve", "gemini")]));

        // Unfinished, not wrong: saved but not offered, so the order boxes are filled in does not decide whether it saves.
        result.ShouldBeSuccess();
        Assert.False(world.Settings.Allows(Capability.Improve));
    }

    [Fact]
    public async Task Handle_ShouldAcceptAJobWithNoProvider_BecauseItSimplyIsNotOffered()
    {
        var world = new World();

        (await world.Handle(Command(
                connections: [Hosted("openai", "sk-one")],
                uses: [Use("improve", provider: "")])))
            .ShouldBeSuccess();
    }

    [Fact]
    public async Task Handle_ShouldRefuse_AProviderThisCannotTalkTo()
    {
        var world = new World();

        (await world.Handle(Command(connections: [Hosted("anthropic", "sk-one")], uses: [])))
            .ShouldBeFailure(AssistanceErrors.UnknownProvider);
    }

    [Fact]
    public async Task Handle_ShouldNotConnectOllamaWithNoAddress_BecauseThereIsNoDefaultThatCouldBeRight()
    {
        var world = new World();

        var result = await world.Handle(Command(
            connections: [new ConnectionEdit("ollama", null, string.Empty)],
            uses: [Use("read", "ollama")]));

        // A row with nothing in it is a provider nobody connected: the screen sends all three every time.
        result.ShouldBeSuccess();
        Assert.Empty(world.Settings.Connections);
        Assert.False(world.Settings.Allows(Capability.Read));
    }

    [Fact]
    public async Task Handle_ShouldRefuse_ABudgetBelowNothing()
    {
        var world = new World();

        (await world.Handle(Command(connections: [], uses: [], monthlyBudget: -1m)))
            .ShouldBeFailure(AssistanceErrors.InvalidBudget);
    }

    [Fact]
    public async Task Handle_ShouldStoreItAsOff_WhenItIsSwitchedOnWithNothingConnected()
    {
        var world = new World();

        var response = (await world.Handle(Command(connections: [], uses: [], enabled: true)))
            .ShouldBeSuccess();

        // Corrected, not refused: the key box may be filled last.
        Assert.False(response.Enabled);
    }

    [Fact]
    public async Task Handle_ShouldLeaveTheSingletonAlone_WhenTheWriteFails()
    {
        var world = new World();
        world.Store.FailWith = SettingsErrors.InvalidValue;

        var result = await world.Handle(Command(
            connections: [Hosted("openai", "sk-one")],
            uses: []));

        // Persist first, then mutate: a failed save must not leave the process talking to an unrecorded provider.
        result.ShouldBeFailure(SettingsErrors.InvalidValue);
        Assert.Empty(world.Settings.Connections);
    }

    [Fact]
    public async Task Handle_ShouldReportEveryBadField_RatherThanTheFirst()
    {
        var world = new World();

        var error = (await world.Handle(Command(
                connections: [Hosted("anthropic", "sk-one")],
                uses: [Use("draw", "ollama")],
                monthlyBudget: -5m)))
            .ShouldBeFailure();

        // Many providers and jobs on screen, so a bare "not valid" would send somebody searching.
        var aggregate = Assert.IsType<ValidationError>(error);
        Assert.Equal(3, aggregate.Errors.Count);
    }

    [Fact]
    public async Task Handle_ShouldSayWhichModelEachJobWouldFallBackTo()
    {
        var world = new World();

        var response = (await world.Handle(Command(
                connections: [Hosted("openai", "sk-one")],
                uses: [Use("improve", "openai")])))
            .ShouldBeSuccess();

        // The server decides the default, so the server says what it is; a hard-coded form name could disagree.
        var improve = response.Uses.Single(one => one.Capability == "improve");
        Assert.Equal("openai-default-improve", improve.DefaultModel);
    }

    private static ConnectionEdit Hosted(string provider, string? apiKey = "sk-secret") =>
        new(provider, apiKey, string.Empty);

    private static ConnectionEdit Local() => new("ollama", null, "http://localhost:11434");

    private static UseEdit Use(string capability, string provider, string model = "") =>
        new(capability, Enabled: true, provider, model);

    private static UpdateAssistanceSettingsCommand Command(
        IReadOnlyList<ConnectionEdit> connections,
        IReadOnlyList<UseEdit> uses,
        bool enabled = false,
        decimal? monthlyBudget = null) =>
        new(enabled, connections, uses, monthlyBudget, PersonalBudget: null);

    /// <summary>The handler and the things it writes through.</summary>
    private sealed class World
    {
        internal AssistanceSettings Settings { get; } = new();

        internal FakeSettingsStore<AssistanceSettings> Store { get; } = new();

        internal FakeSecretProtector Protector { get; } = new();

        internal Task<Result<Contracts.Settings.UpdateAssistance.Response>> Handle(
            UpdateAssistanceSettingsCommand command) =>
            new UpdateAssistanceSettingsCommandHandler(
                    Settings,
                    Store,
                    new FakeAssistants(new FakeAssistant()),
                    Protector)
                .Handle(command, Token);
    }
}
