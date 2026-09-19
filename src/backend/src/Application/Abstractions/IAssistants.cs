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

    /// <summary>
    /// Where this provider is, when nobody overrode it.
    /// </summary>
    /// <param name="kind">Which provider.</param>
    /// <remarks>
    /// Here rather than in settings because it is a fact about the provider
    /// rather than about this installation. Google's address is not a
    /// deployment decision, and asking an administrator for it made connecting
    /// look like more work than it is.
    /// </remarks>
    string HomeOf(AssistantKind kind);

    /// <summary>
    /// Which of its models to use for this job, when nobody chose one.
    /// </summary>
    /// <param name="kind">Which provider.</param>
    /// <param name="capability">Which job.</param>
    /// <remarks>
    /// Per job, not per provider, because drawing and writing are different
    /// models everywhere that does both. An empty choice therefore means
    /// "whatever is currently sensible for this job" rather than "whatever this
    /// build shipped believing".
    /// </remarks>
    string DefaultModelFor(AssistantKind kind, Capability capability);
}
