using System.Text;
using Domain.Recipes;

namespace Infrastructure.Persistence.Recipes;

/// <summary>Creates tags on demand when a recipe is saved with them and removes them when the last recipe drops them.</summary>
/// <param name="executor">Runs the SQL inside the caller's transaction.</param>
internal sealed class TagWriter(DbExecutor executor)
{
    internal async Task LinkAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        // One tag per slug, as an upsert cannot name a row twice.
        var named = recipe.Tags
            .Select(name => (name: name.Trim(), slug: Slugify(name)))
            .Where(tag => tag.slug.Length > 0)
            .DistinctBy(tag => tag.slug)
            .ToArray();

        if (named.Length > 0)
        {
            await executor.ExecuteAsync(
                """
                with upserted as (
                    insert into tags (id, household_id, name, slug)
                    select id, @householdId, name, slug
                    from unnest(@ids::uuid[], @names::text[], @slugs::text[]) as t(id, name, slug)
                    on conflict (household_id, slug) do update set name = tags.name
                    returning id
                )
                insert into recipe_tags (recipe_id, tag_id)
                select @recipeId, id from upserted
                on conflict do nothing;
                """,
                new
                {
                    recipeId = recipe.Id,
                    householdId = recipe.HouseholdId,
                    ids = named.Select(_ => Domain.Shared.CulinaId.New()).ToArray(),
                    names = named.Select(tag => tag.name).ToArray(),
                    slugs = named.Select(tag => tag.slug).ToArray()
                },
                cancellationToken).ConfigureAwait(false);
        }

        await DeleteOrphansAsync(recipe.HouseholdId, cancellationToken).ConfigureAwait(false);
    }

    private Task<int> DeleteOrphansAsync(Guid householdId, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            """
            delete from tags
            where household_id = @householdId
              and not exists (select 1 from recipe_tags where tag_id = tags.id);
            """,
            new { householdId },
            cancellationToken);

    // German umlauts expand ("Süßspeise" becomes "suessspeise") so two spellings of a word are one tag.
    // Other accents use an explicit table because InvariantGlobalization leaves String.Normalize without ICU data.
    internal static string Slugify(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var slug = new StringBuilder(name.Length);

        foreach (var character in name.Trim().ToLowerInvariant())
        {
            var folded = Fold(character);

            if (folded.Length > 0)
            {
                slug.Append(folded);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        return slug.ToString().Trim('-');
    }

    private static string Fold(char character) => character switch
    {
        'ä' => "ae",
        'ö' => "oe",
        'ü' => "ue",
        'ß' => "ss",

        'á' or 'à' or 'â' or 'ã' or 'å' => "a",
        'é' or 'è' or 'ê' or 'ë' => "e",
        'í' or 'ì' or 'î' or 'ï' => "i",
        'ó' or 'ò' or 'ô' or 'õ' => "o",
        'ú' or 'ù' or 'û' => "u",
        'ç' => "c",
        'ñ' => "n",
        'ý' or 'ÿ' => "y",
        'æ' => "ae",
        'ø' => "o",

        _ => char.IsLetterOrDigit(character) ? character.ToString() : string.Empty
    };
}
