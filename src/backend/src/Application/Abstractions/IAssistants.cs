using Domain.Assistance;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Picks the adapter for the provider this instance is connected to.</summary>
public interface IAssistants
{
    /// <summary>The adapter for this provider, or a failure naming it.</summary>
    /// <param name="kind">Which provider.</param>
    Result<IAssistant> For(AssistantKind kind);

    /// <summary>Where this provider is, when nobody overrode it.</summary>
    /// <param name="kind">Which provider.</param>
    string HomeOf(AssistantKind kind);

    /// <summary>Which of its models to use for this job, when nobody chose one.</summary>
    /// <param name="kind">Which provider.</param>
    /// <param name="capability">Which job.</param>
    string DefaultModelFor(AssistantKind kind, Capability capability);
}
