namespace Domain.Suggestions;

/// <summary>Why one recipe was suggested, named after the term that actually decided it.</summary>
/// <remarks>
/// A reason is never composed after the fact: it is whichever scored term dominated, or
/// <see cref="None"/> and the card shows its ordinary meta line. An invented explanation is a lie
/// that discredits every true reason.
/// </remarks>
public enum SuggestionReason
{
    /// <summary>No term dominated. Say nothing.</summary>
    None = 0,

    /// <summary>They cook this one. The card already carries the count.</summary>
    Affinity = 1,

    /// <summary>Liked once, and not made for a long time.</summary>
    Rediscovery = 2,

    /// <summary>It carries a tag they cook a lot of. The tag is the subject.</summary>
    Tag = 3,

    /// <summary>It uses an ingredient they cook a lot with. The name is the subject.</summary>
    Ingredient = 4,

    /// <summary>This household cooks this kind of thing at this time of year.</summary>
    Season = 5,

    /// <summary>It is usually planned in the slot that was asked for.</summary>
    Slot = 6,

    /// <summary>Somebody else in the household cooks it. Their name is the subject.</summary>
    Household = 7,

    /// <summary>New in the book, and nobody has cooked it yet.</summary>
    Fresh = 8,

    /// <summary>It resembles the recipe being looked at.</summary>
    Similar = 9
}
