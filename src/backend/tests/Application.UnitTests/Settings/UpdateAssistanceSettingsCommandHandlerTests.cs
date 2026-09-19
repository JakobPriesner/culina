using Application.Abstractions.Settings;
using Application.Settings.UpdateAssistance;
using Domain.Assistance;
using Domain.Shared;
using TestSupport;

namespace Application.UnitTests.Settings;

/// <summary>
/// Connecting several providers at once, and pointing each job at one.
/// </summary>
public class UpdateAssistanceSettingsCommandHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ShouldConnectSeveralProvidersAtOnce()
    {
        // Arrange
        var world = new World();

        // Act
        var response = (await world.Handle(Command(
                connections: [Hosted("openai", "sk-one"), Hosted("gemini", "sk-two"), Local()],
                uses: [Use("improve", "openai"), Use("draw", "gemini"), Use("read", "ollama")])))
            .ShouldBeSuccess();

        // Assert
        // The whole point: the providers are not interchangeable, and a
        // household with more than one wants each for what it is good at.
        Assert.All(response.Connections, connection => Assert.True(connection.Usable));
        Assert.Equal(3, world.Settings.Connections.Count);
    }

    [Fact]
    public async Task Handle_ShouldSendEachJobToTheProviderItWasPointedAt()
    {
        // Arrange
        var world = new World();

        // Act
        await world.Handle(Command(
            connections: [Hosted("openai", "sk-one"), Hosted("gemini", "sk-two")],
            uses: [Use("improve", "openai", "cheap-one"), Use("draw", "gemini")]));

        // Assert
        Assert.Equal("openai", world.Settings.UseFor(Capability.Improve)!.Provider);
        Assert.Equal("cheap-one", world.Settings.UseFor(Capability.Improve)!.Model);
        Assert.Equal("gemini", world.Settings.UseFor(Capability.Draw)!.Provider);
    }

    [Fact]
    public async Task Handle_ShouldProtectEveryKey_RatherThanStoringWhatWasTyped()
    {
        // Arrange
        var world = new World();

        // Act
        await world.Handle(Command(
            connections: [Hosted("openai", "sk-one"), Hosted("gemini", "sk-two")],
            uses: []));

        // Assert
        Assert.All(
            world.Settings.Connections,
            one => Assert.StartsWith("protected:", one.ProtectedApiKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_ShouldKeepEachStoredKey_WhenTheFormLeavesItOut()
    {
        // Arrange
        var world = new World();
        await world.Handle(Command(
            connections: [Hosted("openai", "sk-one"), Hosted("gemini", "sk-two")],
            uses: []));

        // Act
        // The same form saved again to change a budget, with no keys in it.
        await world.Handle(Command(
            connections: [Hosted("openai", apiKey: null), Hosted("gemini", apiKey: null)],
            uses: [],
            monthlyBudget: 25m));

        // Assert
        Assert.Equal(2, world.Settings.Connections.Count);
        Assert.All(world.Settings.Connections, one => Assert.True(one.HasApiKey));
        Assert.Equal(25m, world.Settings.MonthlyBudget);
    }

    [Fact]
    public async Task Handle_ShouldDisconnectAProvider_WhenItsKeyIsCleared()
    {
        // Arrange
        var world = new World();
        await world.Handle(Command(
            connections: [Hosted("openai", "sk-one"), Hosted("gemini", "sk-two")],
            uses: []));

        // Act
        await world.Handle(Command(
            connections: [Hosted("openai", apiKey: ""), Hosted("gemini", apiKey: null)],
            uses: []));

        // Assert
        // An empty string is somebody clearing the box, which is different from
        // not sending it — and a connection with nothing in it is not stored.
        Assert.Single(world.Settings.Connections);
        Assert.Equal("gemini", world.Settings.Connections[0].Provider);
    }

    [Fact]
    public async Task Handle_ShouldConnectOllamaWithNoKeyAtAll_GivenAnAddress()
    {
        // Arrange
        var world = new World();

        // Act
        var response = (await world.Handle(Command(connections: [Local()], uses: [])))
            .ShouldBeSuccess();

        // Assert
        // The case that breaks "connected means has a key". A model on your own
        // machine has nobody to authenticate to.
        var ollama = response.Connections.Single(one => one.Provider == "ollama");
        Assert.True(ollama.Usable);
        Assert.False(ollama.ApiKeyConfigured);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_PointingDrawingAtAProviderThatCannotDraw()
    {
        // Arrange
        var world = new World();

        // Act
        var result = await world.Handle(Command(
            connections: [Local()],
            uses: [Use("draw", "ollama")]));

        // Assert
        // Caught at the form rather than at the call, so nobody switches on a
        // capability that could never work.
        result.ShouldBeFailure(AssistanceErrors.DrawingNotSupported);
    }

    [Fact]
    public async Task Handle_ShouldAcceptAJobPointedAtAProviderNobodyHasConnectedYet()
    {
        // Arrange
        var world = new World();

        // Act
        var result = await world.Handle(Command(
            connections: [Hosted("openai", "sk-one")],
            uses: [Use("improve", "gemini")]));

        // Assert
        // Unfinished rather than wrong. The job is saved and simply is not
        // offered — refusing it would mean a form where the order you fill the
        // boxes in decides whether it saves.
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
        // Arrange
        var world = new World();

        // Act
        var result = await world.Handle(Command(
            connections: [new ConnectionEdit("ollama", null, string.Empty)],
            uses: [Use("read", "ollama")]));

        // Assert
        // A row with nothing in it is a provider nobody connected, not an
        // error: the screen sends all three every time it saves.
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
        // Arrange
        var world = new World();

        // Act
        var response = (await world.Handle(Command(connections: [], uses: [], enabled: true)))
            .ShouldBeSuccess();

        // Assert
        // Corrected rather than refused: the form lets somebody fill the key
        // box last, and an error about a field they are about to type into
        // helps nobody.
        Assert.False(response.Enabled);
    }

    [Fact]
    public async Task Handle_ShouldLeaveTheSingletonAlone_WhenTheWriteFails()
    {
        // Arrange
        var world = new World();
        world.Store.FailWith = SettingsErrors.InvalidValue;

        // Act
        var result = await world.Handle(Command(
            connections: [Hosted("openai", "sk-one")],
            uses: []));

        // Assert
        // Persist first, then mutate. A failed save must never leave the
        // process talking to a provider the database has not heard of.
        result.ShouldBeFailure(SettingsErrors.InvalidValue);
        Assert.Empty(world.Settings.Connections);
    }

    [Fact]
    public async Task Handle_ShouldReportEveryBadField_RatherThanTheFirst()
    {
        // Arrange
        var world = new World();

        // Act
        var error = (await world.Handle(Command(
                connections: [Hosted("anthropic", "sk-one")],
                uses: [Use("draw", "ollama")],
                monthlyBudget: -5m)))
            .ShouldBeFailure();

        // Assert
        // A screen with three providers and four jobs on it, so "that is not
        // valid" would send somebody looking through all of them.
        var aggregate = Assert.IsType<ValidationError>(error);
        Assert.Equal(3, aggregate.Errors.Count);
    }

    [Fact]
    public async Task Handle_ShouldSayWhichModelEachJobWouldFallBackTo()
    {
        // Arrange
        var world = new World();

        // Act
        var response = (await world.Handle(Command(
                connections: [Hosted("openai", "sk-one")],
                uses: [Use("improve", "openai")])))
            .ShouldBeSuccess();

        // Assert
        // The server decides the default, so the server says what it is — a
        // form hard-coding a name would show a placeholder the server had
        // stopped agreeing with.
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
