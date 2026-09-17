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

    /// <summary>The address of another app could not be read as an address.</summary>
    public static readonly Error InvalidSourceAddress = new(
        "import.invalid_source_address",
        "That is not an address this can connect to. It looks like https://recipes.example.com.",
        ErrorType.Validation);

    /// <summary>The token was empty, or far longer than a token.</summary>
    public static readonly Error InvalidSourceToken = new(
        "import.invalid_source_token",
        "That API token does not look like a token.",
        ErrorType.Validation);

    /// <summary>The name given to a connection was too long.</summary>
    public static readonly Error InvalidSourceLabel = new(
        "import.invalid_source_label",
        "That name is too long for a connection.",
        ErrorType.Validation);

    /// <summary>This does not know how to read that kind of app.</summary>
    public static readonly Error UnknownSourceKind = new(
        "import.unknown_source_kind",
        "This cannot read that kind of app yet.",
        ErrorType.Validation);

    /// <summary>No such connection, or not this household's to see.</summary>
    public static Error SourceNotFound(Guid sourceId) => new(
        "import.source_not_found",
        $"There is no connection {sourceId}.",
        ErrorType.NotFound);

    /// <summary>The same instance is already connected to this household.</summary>
    public static readonly Error SourceAlreadyConnected = new(
        "import.source_already_connected",
        "That app is already connected to this kitchen.",
        ErrorType.Conflict);

    /// <summary>
    /// The other app answered, and said no.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="CouldNotFetch"/> on purpose, and the one import
    /// failure that is specific: a wrong token is the single most likely thing
    /// to go wrong when connecting, and "that did not work" would leave the
    /// person re-checking the address they typed correctly.
    /// </remarks>
    public static readonly Error SourceRefused = new(
        "import.source_refused",
        "That app refused the token. Check that it is current and has permission to read recipes.",
        ErrorType.Validation);

    /// <summary>The other app answered with something this could not read.</summary>
    public static readonly Error SourceNotUnderstood = new(
        "import.source_not_understood",
        "That app answered with something this could not read. It may be a version this does not know.",
        ErrorType.Validation);

    /// <summary>This recipe has already been brought into this kitchen.</summary>
    public static readonly Error AlreadyImported = new(
        "import.already_imported",
        "That recipe has already been brought over.",
        ErrorType.Conflict);

    /// <summary>The recipe was written here rather than imported.</summary>
    public static readonly Error NoOrigin = new(
        "import.no_origin",
        "That recipe was written here.",
        ErrorType.NotFound);

    /// <summary>More recipes were asked for in one go than one request may carry.</summary>
    public static readonly Error TooManyAtOnce = new(
        "import.too_many_at_once",
        "That is more recipes than one request brings over. Ask for fewer.",
        ErrorType.Validation);
}
