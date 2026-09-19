using Domain.Recipes;
using Domain.Shared;

namespace Application.Assistance;

/// <summary>
/// What the model is told to do.
/// </summary>
/// <remarks>
/// <para>
/// Written here and nowhere else. Every one of these strings is the
/// <em>trusted</em> half of a request — the half this application composes —
/// and it never touches the untrusted half. A prompt assembled by concatenating
/// an instruction with a recipe somebody pasted is the injection this feature
/// has to not have, so the two travel in different fields the whole way down
/// and meet only inside the provider.
/// </para>
/// <para>
/// They name the units the app already knows, although the domain accepts
/// others: a unit is any word, so "Schuss" and "fl oz" are perfectly legal and
/// only a digit stuck to a number ("200g") is not. Listing the familiar ones
/// steers a model towards the words the rest of the app can scale and combine,
/// without pretending the vocabulary is closed when it is not.
/// </para>
/// </remarks>
internal static class AssistantPrompts
{
    /// <summary>The units the app already knows, as the model should write them.</summary>
    private static readonly string UnitList =
        string.Join(", ", Unit.BuiltIn.Select(unit => unit.Code));

    /// <summary>Rewrites a recipe somebody already wrote.</summary>
    /// <param name="language">The language the answer must be in.</param>
    /// <remarks>
    /// "Do not invent" is the load-bearing sentence. The point of this
    /// capability is that somebody's own recipe comes back clearer, and a model
    /// that helpfully adds a clove of garlic has changed what they cook rather
    /// than how it reads.
    /// </remarks>
    internal static string Improve(Language language) =>
        $"""
         You are helping somebody tidy up a recipe they wrote themselves, for their
         own kitchen.

         Rewrite it so the steps are in a sensible order, each step is one action,
         the wording is plain and the ingredient list is consistent. Split a step
         that does two things. Give a step a short title only where it genuinely
         names a stage of the work.

         Do not invent anything. Do not add, remove or substitute ingredients, and
         do not change amounts, times or temperatures. If the original is unclear
         about something, keep it unclear rather than deciding for them. You are
         changing how it reads, not what it makes.

         {Common(language)}
         """;

    /// <summary>Writes one from an idea.</summary>
    /// <param name="language">The language the answer must be in.</param>
    internal static string Draft(Language language) =>
        $"""
         You are writing a recipe for somebody cooking at home, from the idea they
         describe.

         Write something they could actually cook this evening: ordinary
         ingredients, ordinary equipment, and steps that say what to do rather
         than what to achieve. Give realistic times. If they named ingredients
         they have, build it around those.

         {Common(language)}
         """;

    /// <summary>Reads one out of a photograph or a block of text.</summary>
    /// <param name="language">The language the answer must be in.</param>
    /// <remarks>
    /// The one prompt that says what to do with a gap, because this is the one
    /// where gaps are normal: a photograph of a cookbook page can be blurred at
    /// the edge, and a guess dressed as a reading is worse than a blank.
    /// </remarks>
    internal static string Read(Language language) =>
        $"""
         You are copying out a recipe from a photograph or from text somebody
         pasted, so that it can be typed into their recipe book.

         Transcribe what is there. Keep their words and their amounts. Do not
         improve the writing, and do not fill in anything the source does not say
         — if an amount, a time or a step is missing or unreadable, leave it out
         rather than guessing. Leaving a gap is the correct answer; inventing a
         plausible number is not.

         If what you are given is not a recipe at all, answer with an empty
         recipe rather than making one up.

         {Common(language)}
         """;

    /// <summary>Describes a dish so a picture can be drawn of it.</summary>
    /// <param name="title">What the recipe is called.</param>
    /// <param name="description">What it says about itself, if anything.</param>
    internal static string Draw(string title, string? description)
    {
        var about = string.IsNullOrWhiteSpace(description) ? string.Empty : $" {description}";

        return $"A photograph of {title}, plated simply on a plain plate, "
            + $"daylight from one side, shot from slightly above, shallow depth of field, "
            + $"nothing else in the frame, no text and no hands.{about}";
    }

    /// <summary>
    /// What every composing prompt ends with.
    /// </summary>
    /// <remarks>
    /// The language instruction is here rather than left to the model's
    /// judgement because the material is the wrong thing to infer it from: a
    /// German household pasting an English page wants a German recipe, and a
    /// model reading the page would answer in English.
    /// </remarks>
    private static string Common(Language language) =>
        $"""
         Write the whole recipe in {Name(language)}, whatever language the material
         is in.

         Prefer these units, written exactly like this: {UnitList}. Another word is
         allowed where none of those fits, but it must be a word — never a unit
         with a digit in it, and never "200g" as one lump. If something has no
         sensible unit, give the amount with no unit at all. Put the amount in the
         quantity field and the preparation ("finely chopped") in the note field,
         never in the name — the name is the shoppable noun on its own.
         """;

    private static string Name(Language language) => language switch
    {
        Language.De => "German",
        _ => "English"
    };
}
