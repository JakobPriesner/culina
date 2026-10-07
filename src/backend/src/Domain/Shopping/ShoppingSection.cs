namespace Domain.Shopping;

/// <summary>Where in a shop a thing is found, ordered the way a shop is walked so a list reads as a route.</summary>
public enum ShoppingSection
{
    /// <summary>Fruit and vegetables.</summary>
    Produce = 0,

    /// <summary>Milk, cheese, eggs.</summary>
    DairyEggs = 1,

    /// <summary>Meat and fish.</summary>
    MeatFish = 2,

    /// <summary>Bread and baked goods.</summary>
    Bakery = 3,

    /// <summary>Flour, rice, pasta, pulses.</summary>
    DryGoods = 4,

    /// <summary>Tins and jars.</summary>
    CannedJars = 5,

    /// <summary>The freezer.</summary>
    Frozen = 6,

    /// <summary>Spices and baking supplies.</summary>
    SpicesBaking = 7,

    /// <summary>Anything drinkable.</summary>
    Drinks = 8,

    /// <summary>Soap, foil, bin bags.</summary>
    Household = 9,

    /// <summary>Everything the keyword table did not recognise.</summary>
    Other = 10
}
