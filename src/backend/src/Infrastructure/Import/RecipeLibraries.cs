using Application.Abstractions;
using Domain.Import;
using Domain.Shared;

namespace Infrastructure.Import;

/// <summary>
/// The list of apps this can read, and the only place it is written down.
/// </summary>
/// <remarks>
/// <para>
/// Adding Mealie is a class beside <c>TandoorLibrary</c>, a value in
/// <see cref="SourceKind"/>, a value in the database's check constraint, and
/// one line here. Nothing in <c>Application</c> changes, which is the whole
/// point of the seam.
/// </para>
/// <para>
/// Built from what the container has rather than from a hard-coded list, so a
/// reader that exists but was never registered fails at this seam with a named
/// error instead of as a null further down.
/// </para>
/// </remarks>
internal sealed class RecipeLibraries(IEnumerable<IRecipeLibrary> readers) : IRecipeLibraries
{
    private readonly Dictionary<SourceKind, IRecipeLibrary> byKind =
        readers.ToDictionary(reader => reader.Kind);

    public Result<IRecipeLibrary> For(SourceKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        return byKind.TryGetValue(kind, out var reader)
            ? Result<IRecipeLibrary>.Success(reader)
            : ImportErrors.UnknownSourceKind;
    }
}
