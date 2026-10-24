namespace Domain.Diary;

/// <summary>When in the day something was eaten. A fourth meal the plan does not have: eating is not buying.</summary>
public enum DiaryMeal
{
    /// <summary>Breakfast.</summary>
    Breakfast,

    /// <summary>Lunch.</summary>
    Lunch,

    /// <summary>Dinner.</summary>
    Dinner,

    /// <summary>Anything between meals.</summary>
    Snack
}

/// <summary>Where an entry's figures come from, which decides how much they can be trusted.</summary>
public enum DiarySource
{
    /// <summary>A portion of a recipe, looked up through its ingredients.</summary>
    Recipe,

    /// <summary>A food from the table or a household's own, by weight or a cited typical unit.</summary>
    Food,

    /// <summary>Figures the person typed, such as a café lunch.</summary>
    Typed
}

/// <summary>The words the API and the database use for meals and sources.</summary>
public static class DiaryCodes
{
    /// <summary>The code of a meal.</summary>
    public static string Of(DiaryMeal meal) => meal switch
    {
        DiaryMeal.Breakfast => "breakfast",
        DiaryMeal.Lunch => "lunch",
        DiaryMeal.Dinner => "dinner",
        _ => "snack"
    };

    /// <summary>The code of a source.</summary>
    public static string Of(DiarySource source) => source switch
    {
        DiarySource.Recipe => "recipe",
        DiarySource.Food => "food",
        _ => "typed"
    };

    /// <summary>The meal a code names, or null.</summary>
    public static DiaryMeal? ToMeal(string? code) => code switch
    {
        "breakfast" => DiaryMeal.Breakfast,
        "lunch" => DiaryMeal.Lunch,
        "dinner" => DiaryMeal.Dinner,
        "snack" => DiaryMeal.Snack,
        _ => null
    };

    /// <summary>The source a code names; an unknown one reads as typed, the least trusted.</summary>
    public static DiarySource ToSource(string? code) => code switch
    {
        "recipe" => DiarySource.Recipe,
        "food" => DiarySource.Food,
        _ => DiarySource.Typed
    };
}
