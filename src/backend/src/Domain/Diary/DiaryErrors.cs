using Domain.Shared;

namespace Domain.Diary;

/// <summary>Failures from the food diary.</summary>
public static class DiaryErrors
{
    /// <summary>The diary is switched off for this person, so there is nothing to read or write.</summary>
    public static readonly Error Disabled = new(
        "diary.disabled",
        "Your food diary is switched off. Switch it on in the settings first.",
        ErrorType.Forbidden);

    /// <summary>Switching on without agreeing to the current wording.</summary>
    public static readonly Error ConsentRequired = new(
        "diary.consent_required",
        "Switching the diary on needs your agreement to the current wording.",
        ErrorType.Validation);

    /// <summary>A target that is not a plausible daily amount.</summary>
    public static readonly Error InvalidTarget = new(
        "diary.invalid_target",
        "That is not a plausible daily target.",
        ErrorType.Validation);

    /// <summary>A headline the diary does not know.</summary>
    public static readonly Error InvalidHeadline = new(
        "diary.invalid_headline",
        "The headline shows what was eaten or what is left.",
        ErrorType.Validation);

    /// <summary>A day outside what the diary can hold, or a range that is backwards or too long.</summary>
    public static readonly Error InvalidDates = new(
        "diary.invalid_dates",
        "Those dates do not make a range the diary can show.",
        ErrorType.Validation);

    /// <summary>A meal the diary does not know.</summary>
    public static readonly Error InvalidMeal = new(
        "diary.invalid_meal",
        "Breakfast, lunch, dinner or a snack.",
        ErrorType.Validation);

    /// <summary>An amount that is not a plausible portion of food.</summary>
    public static readonly Error InvalidAmount = new(
        "diary.invalid_amount",
        "That is not a plausible amount to have eaten.",
        ErrorType.Validation);

    /// <summary>A typed entry without a name or with an implausible figure.</summary>
    public static readonly Error Implausible = new(
        "diary.implausible",
        "A typed entry needs a name and figures someone could have eaten.",
        ErrorType.Validation);

    /// <summary>The request names no recipe, food or typed figures, or more than one.</summary>
    public static readonly Error UnclearSource = new(
        "diary.unclear_source",
        "Say what was eaten: a recipe, a food or your own figures.",
        ErrorType.Validation);

    /// <summary>The food code is neither in the food table nor one of the household's own.</summary>
    public static readonly Error FoodUnknown = new(
        "diary.food_unknown",
        "That food is not in the food table or among your household's own.",
        ErrorType.NotFound);

    /// <summary>The unit has no known weight for that food.</summary>
    public static readonly Error UnitUnknown = new(
        "diary.unit_unknown",
        "There is no known weight for that unit of this food. Enter it in grams.",
        ErrorType.Validation);

    /// <summary>No such entry, or none of this person's.</summary>
    public static readonly Error EntryNotFound = new(
        "diary.entry_not_found",
        "That entry is not in your diary.",
        ErrorType.NotFound);
}
