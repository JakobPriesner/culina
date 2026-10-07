using Application.Abstractions;
using Domain.Import;
using Domain.Shared;

namespace Infrastructure.Import;

/// <summary>
/// The apps this can read, and the only place they are written down. Built from the container so an unregistered reader fails here
/// with a named error rather than as a null further down.
/// </summary>
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
