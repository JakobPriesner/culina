using Domain.Shared;

namespace Domain.Import;

/// <summary>
/// What can go wrong fetching a recipe from somebody else's website.
/// </summary>
/// <remarks>
/// Every one of these is deliberately vague about <em>why</em> an address was
/// refused. Telling a caller that 10.0.0.5 was blocked but 10.0.0.6 timed out
/// turns this endpoint into a port scanner for the network the server sits in.
/// </remarks>
public static class ImportErrors
{
    /// <summary>The address is not one this can fetch.</summary>
    public static readonly Error UnreachableAddress = new(
        "import.unreachable_address",
        "That address cannot be fetched. It has to be an ordinary public web page.",
        ErrorType.Validation);

    /// <summary>The page did not answer, or took too long.</summary>
    public static readonly Error CouldNotFetch = new(
        "import.could_not_fetch",
        "That page could not be read. It may be gone, or slow, or refusing visitors.",
        ErrorType.Validation);

    /// <summary>The page answered with something that is not a web page.</summary>
    public static readonly Error NotAWebPage = new(
        "import.not_a_web_page",
        "That address is not a web page.",
        ErrorType.Validation);

    /// <summary>The page is larger than this will read.</summary>
    public static readonly Error TooLarge = new(
        "import.too_large",
        "That page is too large to read.",
        ErrorType.Validation);
}
