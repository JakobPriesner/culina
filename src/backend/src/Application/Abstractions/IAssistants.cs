using Domain.Assistance;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>
/// Picks the adapter for the provider this instance is connected to.
/// </summary>
/// <remarks>
/// A registry rather than a switch inside each handler, exactly as
/// <see cref="IRecipeLibraries"/> is: the handlers stay free of the list, and
/// adding a third provider is a class in <c>Infrastructure</c> and one line of
/// registration. An adapter that is written but never registered fails here,
/// with a named error, rather than as a null somewhere deeper.
/// </remarks>
public interface IAssistants
{
    /// <summary>The adapter for this provider, or a failure naming it.</summary>
    /// <param name="kind">Which provider.</param>
    Result<IAssistant> For(AssistantKind kind);
}
