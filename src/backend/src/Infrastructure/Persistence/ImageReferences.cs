namespace Infrastructure.Persistence;

/// <summary>
/// Every column that points into the content-addressed image store. One file can serve several rows, so
/// deletability is asked here once; a new table keeping an image hash belongs here.
/// </summary>
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
