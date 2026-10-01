using Api.Endpoints.RecoveryCodes.Issue.V1;

namespace Api.Endpoints.RecoveryCodes;

/// <summary>The recovery codes domain's endpoints, registered explicitly.</summary>
internal static class RecoveryCodesEndpoints
{
    internal static IServiceCollection AddRecoveryCodesEndpoints(this IServiceCollection services) =>
        services.AddSingleton<IEndpoint, IssueRecoveryCodeEndpoint>();
}
