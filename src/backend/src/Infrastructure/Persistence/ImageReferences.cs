namespace Infrastructure.Persistence;

/// <summary>
/// Every column that points into the content-addressed image store.
/// </summary>
/// <remarks>
/// <para>
/// One file can serve several rows: the same bytes re-encode to the same hash,
/// so a photograph used as a recipe's picture and as a cook photo is one file.
/// Whether a file may be deleted is therefore a question about every column
/// that can hold its hash, and it is answered here once.
/// </para>
/// <para>
/// It was answered twice before, and the two answers drifted: removing a
/// recipe's picture asked only about recipe pictures and deleted a cook photo
/// that shared the file. A new table that keeps an image hash belongs here.
/// </para>
/// </remarks>
internal static class ImageReferences
{
    /// <summary>A condition that holds while anything still points at the image.</summary>
    /// <param name="hash">The SQL expression for the hash: a parameter or a column.</param>
    internal static string StillUsed(string hash) =>
        $"""
         (exists (select 1 from recipe_images where content_hash = {hash})
          or exists (select 1 from cook_log_entries where image_hash = {hash}))
         """;
}
