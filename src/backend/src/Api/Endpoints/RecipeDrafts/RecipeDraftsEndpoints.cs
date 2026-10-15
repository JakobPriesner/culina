using Api.Endpoints.RecipeDrafts.Compose.V1;
using Api.Endpoints.RecipeDrafts.FromMedia.V1;
using Api.Endpoints.RecipeDrafts.FromPhotograph.V1;

namespace Api.Endpoints.RecipeDrafts;

/// <summary>The assistant's recipe endpoints, registered explicitly.</summary>
internal static class RecipeDraftsEndpoints
{
    internal static IServiceCollection AddRecipeDraftsEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, ComposeRecipeDraftEndpoint>()
            .AddSingleton<IEndpoint, ReadRecipeDraftEndpoint>()
            .AddSingleton<IEndpoint, ReadMediaRecipeDraftEndpoint>()
            .AddSingleton<IEndpoint, Job.V1.StartRecipeDraftEndpoint>()
            .AddSingleton<IEndpoint, StartIntake.V1.StartRecipeIntakeEndpoint>()
            .AddSingleton<IEndpoint, RetryIntake.V1.RetryRecipeIntakeEndpoint>()
            .AddSingleton<IEndpoint, ListIntakes.V1.ListRecipeIntakesEndpoint>()
            .AddSingleton<IEndpoint, WatchIntakes.V1.WatchRecipeIntakesEndpoint>()
            .AddSingleton<IEndpoint, GetIntake.V1.GetRecipeIntakeEndpoint>()
            .AddSingleton<IEndpoint, GetIntakePhoto.V1.GetRecipeIntakePhotoEndpoint>()
            .AddSingleton<IEndpoint, ReviewIntake.V1.ReviewRecipeIntakeEndpoint>();
}
