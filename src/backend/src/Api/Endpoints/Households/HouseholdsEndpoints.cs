using Api.Endpoints.Households.Archive.V1;
using Api.Endpoints.Households.ChangeMemberRole.V1;
using Api.Endpoints.Households.Create.V1;
using Api.Endpoints.Households.CreateInvitation.V1;
using Api.Endpoints.Households.Delete.V1;
using Api.Endpoints.Households.GetAll.V1;
using Api.Endpoints.Households.GetById.V1;
using Api.Endpoints.Households.GetInvitations.V1;
using Api.Endpoints.Households.GetMembers.V1;
using Api.Endpoints.Households.RemoveMember.V1;
using Api.Endpoints.Households.Rename.V1;
using Api.Endpoints.Households.RevokeInvitation.V1;
using Api.Endpoints.Households.SetInheritance.V1;

namespace Api.Endpoints.Households;

/// <summary>The households domain's endpoints, registered explicitly.</summary>
internal static class HouseholdsEndpoints
{
    internal static IServiceCollection AddHouseholdsEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetHouseholdsEndpoint>()
            .AddSingleton<IEndpoint, CreateHouseholdEndpoint>()
            .AddSingleton<IEndpoint, GetHouseholdEndpoint>()
            .AddSingleton<IEndpoint, RenameHouseholdEndpoint>()
            .AddSingleton<IEndpoint, SetInheritanceEndpoint>()
            .AddSingleton<IEndpoint, DeleteHouseholdEndpoint>()
            .AddSingleton<IEndpoint, GetMembersEndpoint>()
            .AddSingleton<IEndpoint, ChangeMemberRoleEndpoint>()
            .AddSingleton<IEndpoint, RemoveMemberEndpoint>()
            .AddSingleton<IEndpoint, CreateInvitationEndpoint>()
            .AddSingleton<IEndpoint, GetInvitationsEndpoint>()
            .AddSingleton<IEndpoint, RevokeInvitationEndpoint>()
            .AddSingleton<IEndpoint, ExportArchiveEndpoint>()
            .AddSingleton<IEndpoint, RestoreArchiveEndpoint>();
}
