using Application.Abstractions;
using Domain.Assistance;
using Domain.Shared;

namespace Infrastructure.Assistance;

/// <summary>Picks the adapter for a provider.</summary>
/// <remarks>
/// Built from whatever was registered, exactly as <c>RecipeLibraries</c> is, so
/// an adapter that exists but was never registered fails here with a named
/// error rather than as a null somewhere deeper.
/// </remarks>
/// <param name="assistants">Every adapter that was registered.</param>
internal sealed class Assistants(IEnumerable<IAssistant> assistants) : IAssistants
{
    private readonly Dictionary<string, IAssistant> byKind =
        assistants.ToDictionary(assistant => assistant.Kind.Code, StringComparer.Ordinal);

    public Result<IAssistant> For(AssistantKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        return byKind.TryGetValue(kind.Code, out var assistant)
            ? Result<IAssistant>.Success(assistant)
            : AssistanceErrors.UnknownProvider;
    }

    public string HomeOf(AssistantKind kind) => AssistantDefaults.Home(kind);

    public string DefaultModelFor(AssistantKind kind, Capability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        return capability == Capability.Draw
            ? AssistantDefaults.DrawModel(kind)
            : AssistantDefaults.ComposeModel(kind);
    }
}
