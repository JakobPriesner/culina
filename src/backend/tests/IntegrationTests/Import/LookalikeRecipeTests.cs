using System.Globalization;
using System.Text;
using Application.Abstractions;
using Domain.Search;
using IntegrationTests.Fixtures;
using IntegrationTests.Recipes.Evaluation;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Import;

/// <summary>
/// How well an import tells a recipe the household already has from one it
/// does not, measured against the golden library.
/// </summary>
/// <remarks>
/// <para>
/// Two numbers, and they are not worth the same. A missed duplicate is a
/// second Bolognese somebody deletes; a wrong warning is a recipe held back
/// for no reason, from a feature that then stops being believed. So the bar is
/// recall of at least 0.9 and not one false positive.
/// </para>
/// <para>
/// The near misses are the point of the list: the same dish with one thing
/// changed, the same ingredients in a different dish, a name that contains
/// another. Pairs a careful person could argue either way — Chicken Tikka and
/// Chicken Tikka Masala — are left out rather than ruled on here.
/// </para>
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class LookalikeRecipeTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>What an import brings, and which recipe it duplicates, if any.</summary>
    private static readonly Case[] Cases =
    [
        // The same recipe again.
        new("Spaghetti Bolognese", ["pasta", "italienisch"],
            ["Hackfleisch", "passierte Tomaten", "Zwiebel", "Spaghetti", "Möhre"], "Spaghetti Bolognese"),
        new("spaghetti bolognese", [], ["Hackfleisch", "Tomaten", "Zwiebel"], "Spaghetti Bolognese"),
        new("Kaesekuchen", ["backen"], ["Quark", "Eier", "Zucker", "Mürbeteig"], "Käsekuchen"),
        new("Chicken Curry", ["asian"], ["chicken breast", "coconut milk", "curry paste", "rice"], "Chicken Curry"),
        new("Chili con Carne", ["mexikanisch"],
            ["Hackfleisch", "Kidneybohnen", "Mais", "Chili", "Zwiebel"], "Chili con Carne"),

        // Nearly the same name, nearly the same ingredients.
        new("Omas Spaghetti Bolognese", ["pasta"],
            ["Hackfleisch", "passierte Tomaten", "Zwiebel", "Spaghetti", "Möhre", "Knoblauch"],
            "Spaghetti Bolognese"),
        new("Tiramisu classico", ["dessert"],
            ["Mascarpone", "Löffelbiskuits", "Espresso", "Eier", "Kakao"], "Tiramisu"),
        new("Hähnchen Curry", ["asiatisch"],
            ["Hähnchenschenkel", "Kokosmilch", "Currypaste", "Reis"], "Hähnchen-Curry"),
        new("Rindergulasch nach Omas Art", ["deftig"],
            ["Rindfleisch", "Zwiebeln", "Paprikapulver", "Tomatenmark", "Kümmel"], "Rindergulasch"),

        // The same thing under a name written apart.
        new("Linsen Suppe", ["suppe"], ["Linsen", "Karotten", "Speck", "Kartoffeln"], "Linsensuppe"),

        // Near misses: nothing here is a recipe the library already has.
        new("Spaghetti Carbonara", ["pasta", "italienisch"], ["Spaghetti", "Speck", "Eier", "Parmesan"], null),
        new("Lasagne mit Spinat", ["pasta", "ofen"],
            ["Spinat", "Ricotta", "Lasagneplatten", "Tomaten", "Béchamel"], null),
        new("Kartoffelsuppe", ["suppe"], ["Kartoffeln", "Zwiebel", "Gemüsebrühe", "Sahne"], null),
        new("Hähnchen mit Reis", [], ["Hähnchenschenkel", "Reis", "Paprika"], null),
        new("Apfelstrudel", ["backen"], ["Äpfel", "Mehl", "Butter", "Zucker", "Zimt"], null),
        new("Rührei mit Speck", ["frühstück"], ["Eier", "Butter", "Speck"], null),
        new("Pilzpfanne", [], ["Champignons", "Zwiebeln", "Sahne", "Petersilie"], null),
        new("Pfannkuchen mit Apfel", ["frühstück"], ["Mehl", "Eier", "Milch", "Äpfel", "Zucker"], null),
        new("Mushroom Soup", ["soup"], ["mushrooms", "onion", "cream", "vegetable stock"], null)
    ];

    [Fact]
    public async Task Lookalikes_ShouldFindTheDuplicates_AndNeverWarnAboutARecipeThatIsNotOne()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await GoldenLibrary.Load().SeedAsync(kitchen);
        var me = await kitchen.Client.GetAsync("/api/v1/users/me", Token);
        var userId = me.Json!.Value.GetProperty("userId").GetGuid();

        // Act
        var found = new List<(Case Case, string? Title)>();

        await using (var scope = postgres.Api.Services.CreateAsyncScope())
        {
            var lookalikes = scope.ServiceProvider.GetRequiredService<ILookalikeRecipes>();

            foreach (var one in Cases)
            {
                var alike = await lookalikes.FindAsync(
                    kitchen.HouseholdId,
                    userId,
                    new LookalikeCandidate(
                        one.Title,
                        one.Ingredients,
                        CulinaryLexicon.Describe(one.Title, one.Tags, one.Ingredients)),
                    Token);

                found.Add((one, alike?.Title));
            }
        }

        // Assert
        var duplicates = found.Where(one => one.Case.Duplicates is not null).ToList();
        var recall = duplicates.Count(one => one.Title == one.Case.Duplicates) / (double)duplicates.Count;
        var wrong = found.Where(one => one.Title is not null && one.Title != one.Case.Duplicates).ToList();
        var report = Report(found, recall);

        TestContext.Current.SendDiagnosticMessage(report);

        Assert.True(wrong.Count == 0, report);
        Assert.True(recall >= 0.9, report);
    }

    private static string Report(List<(Case Case, string? Title)> found, double recall)
    {
        var report = new StringBuilder();
        report.AppendLine(
            CultureInfo.InvariantCulture,
            $"Lookalikes over the golden library: recall {recall:F2}");

        foreach (var (one, title) in found)
        {
            var verdict = title == one.Duplicates ? "ok " : "BAD";
            report.AppendLine(
                CultureInfo.InvariantCulture,
                $"  {verdict} {one.Title} → {title ?? "—"} (expected {one.Duplicates ?? "—"})");
        }

        return report.ToString();
    }

    private sealed record Case(string Title, string[] Tags, string[] Ingredients, string? Duplicates);
}
