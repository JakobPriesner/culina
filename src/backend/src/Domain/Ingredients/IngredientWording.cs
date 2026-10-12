namespace Domain.Ingredients;

/// <summary>
/// An ingredient name read back in the words it was written in, so the cook can see what was understood:
/// "fein gehackte rote Zwiebeln" is understood as "Zwiebeln, rot".
/// </summary>
/// <remarks>Read by <see cref="IngredientWords.Wording"/>.</remarks>
/// <param name="Understood">
/// The thing and its kinds, which is what lines of it are merged and looked up as: "Kokosmilch, light" for
/// "Kokosmilch light", "Zwiebeln" for "große Zwiebeln".
/// </param>
/// <param name="Name">The name without what is done to it in the kitchen ("rote Zwiebeln"), or null when nothing is.</param>
/// <param name="Note">What is done to it, for the note ("fein gehackt"), or null when nothing is.</param>
public sealed record IngredientWording(string Understood, string? Name, string? Note);
