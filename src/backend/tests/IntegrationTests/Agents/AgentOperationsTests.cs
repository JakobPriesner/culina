using Api.Agents;
using Api.Extensions;
using IntegrationTests.Fixtures;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Agents;

/// <summary>
/// Every API operation is decided on for agents: offered as a tool (<see cref="AgentOperations"/>) or withheld here,
/// with the reason. A new endpoint fails this until someone chooses, so nothing reaches an agent by default.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class AgentOperationsTests(PostgresFixture postgres)
{
    private static readonly HashSet<string> Withheld = new(StringComparer.Ordinal)
    {
        // The account and its credentials: an agent must not sign anyone in or out, or take the account over.
        "registerUserV1",
        "updateCurrentUserV1",
        "updateUserSettingsV1",
        "changePasswordV1",
        "getRecoveryCodesV1",
        "createRecoveryCodesV1",
        "signInV1",
        "getSessionsV1",
        "signOutV1",
        "revokeSessionV1",
        "createPasswordResetV1",
        "issueRecoveryCodeV1",
        "createPasswordResetLinkV1",
        "createSignInLinkV1",
        "redeemSignInLinkV1",
        "createEmailConfirmationV1",

        // Approving apps that connect agents: an agent must not let more agents in.
        "getAgentAuthorizationByIdV1",
        "decideAgentAuthorizationV1",

        // A device's push registrations.
        "getPushKeyV1",
        "registerPushV1",
        "removePushV1",
        "getPushRelayV1",
        "registerPushDeviceV1",
        "removePushDeviceV1",

        // The shape of a household and who is in it: an invitation would hand the kitchen to a stranger.
        "createHouseholdV1",
        "renameHouseholdV1",
        "deleteHouseholdV1",
        "restoreHouseholdV1",
        "setHouseholdInheritanceV1",
        "getHouseholdHeirsV1",
        "removeHouseholdHeirV1",
        "getHouseholdMembersV1",
        "changeHouseholdMemberRoleV1",
        "removeHouseholdMemberV1",
        "createHouseholdInvitationV1",
        "getHouseholdInvitationsV1",
        "revokeHouseholdInvitationV1",
        "getInvitationByCodeV1",
        "redeemInvitationV1",

        // A whole household at once, as a file.
        "exportArchiveV1",
        "restoreArchiveV1",

        // The instance's settings, which are an administrator's.
        "getRegistrationSettingsV1",
        "updateRegistrationSettingsV1",
        "getAssistanceSettingsV1",
        "updateAssistanceSettingsV1",
        "getMailSettingsV1",
        "updateMailSettingsV1",
        "sendMailTestV1",
        "getAssistanceUsageV1",
        "getAssistanceModelsV1",
        "getServerSettingsV1",
        "updateServerSettingsV1",
        "getSetupV1",
        "getDatabaseSettingsV1",
        "updateDatabaseSettingsV1",
        "getRegistrationPolicyV1",

        // Pictures travel as files or a stream, which a tool cannot carry.
        "setRecipeImageV1",
        "drawRecipeImageV1",
        "getRecipeImageV1",
        "setCookPhotoV1",
        "getCookPhotoV1",

        // Share links publish a recipe to anyone holding the link.
        "getRecipeShareV1",
        "createRecipeShareV1",
        "revokeRecipeShareV1",
        "getSharedRecipeV1",
        "getSharedRecipeImageV1",
        "getSharedRecipeNutritionV1",

        // Connected libraries hold another app's credentials.
        "connectRecipeSourceV1",
        "getRecipeSourcesV1",
        "disconnectRecipeSourceV1",
        "browseRecipeSourceV1",
        "importFromRecipeSourceV1",
        "watchRecipeImportV1",

        // The assistant costs money per call, and an agent is a model already; photographs and media are files.
        "composeRecipeDraftV1",
        "readRecipeDraftV1",
        "readMediaRecipeDraftV1",
        "startRecipeDraftV1",
        "startRecipeIntakeV1",
        "listRecipeIntakesV1",
        "retryRecipeIntakeV1",
        "watchRecipeIntakesV1",
        "getRecipeIntakeV1",
        "getRecipeIntakePhotoV1",
        "reviewRecipeIntakeV1",

        // What a household says a barcode is: it serves the scanner, and is not yet decided on for agents.
        "getHouseholdProductV1",
        "listHouseholdProductsV1",
        "setHouseholdProductV1",
        "removeHouseholdProductV1",

        // What the web app reports about itself and what it showed.
        "recordSuggestionImpressionsV1",
        "createLogRecordsV1"
    };

    [Fact]
    public async Task EveryOperation_ShouldBeOfferedToAgents_OrWithheldWithAReason()
    {
        // Arrange
        var operations = await OperationsAsync();

        // Act
        var undecided = operations.Where(name => !AgentOperations.Allowed.Contains(name) && !Withheld.Contains(name));

        // Assert
        Assert.Empty(undecided);
    }

    [Fact]
    public async Task EveryOfferedOperation_ShouldExist_AndNotAlsoBeWithheld()
    {
        // Arrange
        var operations = await OperationsAsync();

        // Act
        var missing = AgentOperations.Allowed.Where(name => !operations.Contains(name));
        var both = AgentOperations.Allowed.Where(Withheld.Contains);

        // Assert
        Assert.Empty(missing);
        Assert.Empty(both);
    }

    private async Task<HashSet<string>> OperationsAsync()
    {
        var document = await postgres.Api.Services
            .GetRequiredKeyedService<IOpenApiDocumentProvider>(OpenApiExtensions.DocumentName)
            .GetOpenApiDocumentAsync(TestContext.Current.CancellationToken);

        return document.Paths.Values
            .SelectMany(path => path.Operations?.Values.AsEnumerable() ?? [])
            .Select(operation => operation.OperationId!)
            .ToHashSet(StringComparer.Ordinal);
    }
}
