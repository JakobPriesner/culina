using Application.Abstractions.Settings;
using Application.Settings.UpdateAssistance;
using Domain.Assistance;
using Domain.Shared;
using TestSupport;

namespace Application.UnitTests.Settings;

/// <summary>
/// What connecting a model will and will not accept, and what it does with the
/// one value that must never come back out.
/// </summary>
public class UpdateAssistanceSettingsCommandHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ShouldProtectTheKey_RatherThanStoringWhatWasTyped()
    {
        // Arrange
        var world = new World();

        // Act
        await world.Handle(Command(apiKey: "sk-secret"));

        // Assert
        // What is stored went through the protector — in both places, because
        // the row and the singleton are written from the same value. The fake
        // wraps rather than encrypts so that a test can read it back; that the
        // real one is unreadable is data protection's business, not this
        // handler's.
        Assert.NotEqual("sk-secret", world.Settings.ProtectedApiKey);
        Assert.Equal(world.Settings.ProtectedApiKey, world.Store.Saved!.ProtectedApiKey);
        Assert.Equal("sk-secret", world.Protector.Unprotect(world.Settings.ProtectedApiKey));
    }

    [Fact]
    public async Task Handle_ShouldNeverReturnTheKey_OnlyThatThereIsOne()
    {
        // Arrange
        var world = new World();

        // Act
        var response = (await world.Handle(Command(apiKey: "sk-secret"))).ShouldBeSuccess();

        // Assert
        // The response type has no field that could carry it, which is the
        // point of it being a contract type rather than the record.
        Assert.True(response.ApiKeyConfigured);
    }

    [Fact]
    public async Task Handle_ShouldKeepTheStoredKey_WhenTheFormLeavesItOut()
    {
        // Arrange
        // A key is already set, and somebody saves the form to change a budget.
        var world = new World();
        await world.Handle(Command(apiKey: "sk-first"));
        var stored = world.Settings.ProtectedApiKey;

        // Act
        await world.Handle(Command(apiKey: null, monthlyBudget: 5m));

        // Assert
        // Null means "leave it alone". A form that wiped the key every time it
        // was saved for an unrelated reason would be unusable.
        Assert.Equal(stored, world.Settings.ProtectedApiKey);
        Assert.Equal(5m, world.Settings.MonthlyBudget);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldTakeTheKeyAway_WhenAnEmptyOneIsSentDeliberately(string sent)
    {
        // Arrange
        var world = new World();
        await world.Handle(Command(apiKey: "sk-first"));

        // Act
        await world.Handle(Command(apiKey: sent));

        // Assert
        // The third state, and the only way to disconnect: an empty string is
        // somebody clearing the box, which is different from not sending it.
        Assert.False(world.Settings.HasApiKey);
    }

    [Fact]
    public async Task Handle_ShouldStoreItAsOff_WhenItIsSwitchedOnWithNoKey()
    {
        // Arrange
        var world = new World();

        // Act
        var response = (await world.Handle(Command(enabled: true, apiKey: null))).ShouldBeSuccess();

        // Assert
        // Corrected rather than refused: the form lets somebody fill the key box
        // last, and an error about a field they are about to type into helps
        // nobody. There is simply nothing for it to be on with.
        Assert.False(response.Enabled);
        Assert.False(world.Settings.Enabled);
    }

    [Fact]
    public async Task Handle_ShouldConnectOllamaWithNoKeyAtAll_GivenAnAddress()
    {
        // Arrange
        var world = new World();

        // Act
        var response = (await world.Handle(Command(
                enabled: true,
                provider: "ollama",
                apiKey: null,
                baseUrl: "http://localhost:11434")))
            .ShouldBeSuccess();

        // Assert
        // The case that breaks "configured means has a key". A model on your
        // own machine has nobody to authenticate to, and refusing to switch it
        // on for want of a credential it does not use would make the one
        // provider a self-hosted app most obviously wants unusable.
        Assert.True(response.Enabled);
        Assert.True(response.Connected);
        Assert.False(response.ApiKeyConfigured);
    }

    [Fact]
    public async Task Handle_ShouldRefuseOllamaWithNoAddress_BecauseThereIsNoDefaultThatCouldBeRight()
    {
        // Arrange
        var world = new World();

        // Act
        var result = await world.Handle(Command(provider: "ollama", baseUrl: ""));

        // Assert
        // Empty here is not "use the usual address": a model on your own
        // hardware is wherever you put it.
        result.ShouldBeFailure(AssistanceErrors.AddressRequired);
    }

    [Fact]
    public async Task Handle_ShouldStoreOllamaAsOff_WhenItIsSwitchedOnWithNoAddress()
    {
        // Arrange
        var world = new World();

        // Act
        await world.Handle(Command(provider: "openai", apiKey: "sk-first", enabled: true));
        var result = await world.Handle(
            Command(provider: "ollama", baseUrl: "http://localhost:11434"));

        // Assert
        // Switching provider leaves the old key stored but no longer relevant:
        // what counts as connected is the new provider's question.
        result.ShouldBeSuccess();
        Assert.True(world.Settings.IsConnected);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_AProviderThisCannotTalkTo()
    {
        // Arrange
        var world = new World();

        // Act
        var result = await world.Handle(Command(provider: "anthropic"));

        // Assert
        // Quietly picking one would send somebody's key to a company they did
        // not choose.
        result.ShouldBeFailure(AssistanceErrors.UnknownProvider);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_ABudgetBelowNothing()
    {
        var world = new World();

        (await world.Handle(Command(monthlyBudget: -1m)))
            .ShouldBeFailure(AssistanceErrors.InvalidBudget);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_AKeyLongerThanAnyKey()
    {
        var world = new World();

        (await world.Handle(Command(apiKey: new string('k', AssistanceSettings.MaxApiKeyLength + 1))))
            .ShouldBeFailure(AssistanceErrors.InvalidApiKey);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_AnAddressThatIsNotOne()
    {
        var world = new World();

        (await world.Handle(Command(baseUrl: "not an address")))
            .ShouldBeFailure(AssistanceErrors.InvalidBaseUrl);
    }

    [Fact]
    public async Task Handle_ShouldAcceptAPrivateAddress_BecauseAnAdministratorTypedIt()
    {
        // Arrange
        var world = new World();

        // Act
        var result = await world.Handle(Command(baseUrl: "http://localhost:11434"));

        // Assert
        // Unlike the import path, which refuses private addresses because a
        // user types those. Pointing at a model on the same machine is the
        // ordinary reason to set this at all.
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task Handle_ShouldLeaveTheSingletonAlone_WhenTheWriteFails()
    {
        // Arrange
        var world = new World();
        world.Store.FailWith = SettingsErrors.InvalidValue;

        // Act
        var result = await world.Handle(Command(apiKey: "sk-secret"));

        // Assert
        // Persist first, then mutate. A failed save must never leave the
        // process talking to a provider the database has not heard of.
        result.ShouldBeFailure(SettingsErrors.InvalidValue);
        Assert.False(world.Settings.HasApiKey);
    }

    [Fact]
    public async Task Handle_ShouldReportEveryBadField_RatherThanTheFirst()
    {
        // Arrange
        var world = new World();

        // Act
        var error = (await world.Handle(
            Command(provider: "anthropic", baseUrl: "nonsense", monthlyBudget: -5m)))
            .ShouldBeFailure();

        // Assert
        // A form with twelve controls on it, so "that is not valid" would send
        // somebody looking through all of them.
        var aggregate = Assert.IsType<ValidationError>(error);
        Assert.Equal(3, aggregate.Errors.Count);
    }

    private static UpdateAssistanceSettingsCommand Command(
        bool enabled = false,
        string provider = "openai",
        string? apiKey = null,
        string baseUrl = "",
        decimal? monthlyBudget = null) =>
        new(
            enabled,
            provider,
            apiKey,
            baseUrl,
            ComposeModel: "gpt-4o-mini",
            DrawModel: "gpt-image-1",
            ImproveEnabled: true,
            DraftEnabled: true,
            ReadEnabled: true,
            DrawEnabled: false,
            monthlyBudget,
            PersonalBudget: null);

    /// <summary>The handler and the two things it writes through.</summary>
    private sealed class World
    {
        internal AssistanceSettings Settings { get; } = new();

        internal FakeSettingsStore<AssistanceSettings> Store { get; } = new();

        internal FakeSecretProtector Protector { get; } = new();

        internal Task<Result<Contracts.Settings.UpdateAssistance.Response>> Handle(
            UpdateAssistanceSettingsCommand command) =>
            new UpdateAssistanceSettingsCommandHandler(Settings, Store, Protector)
                .Handle(command, Token);
    }
}
