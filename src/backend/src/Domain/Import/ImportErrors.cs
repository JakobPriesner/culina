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

    /// <summary>The app will not trade a sign-in for a token.</summary>
    /// <remarks>
    /// Not always a misconfiguration: an instance whose accounts are all single
    /// sign-on has no password to give, and the answer is to paste a token
    /// rather than to fix anything.
    /// </remarks>
    public static readonly Error SignInNotPossible = new(
        "import.sign_in_not_possible",
        "That app would not sign in with a name and password. Make an API token there and paste it instead.",
        ErrorType.Validation);

    /// <summary>Exactly one way of getting in has to be given.</summary>
    public static readonly Error AmbiguousCredentials = new(
        "import.ambiguous_credentials",
        "Give either a name and password or an API token, not both.",
        ErrorType.Validation);

    /// <summary>What was fetched for a picture is not a picture.</summary>
    /// <remarks>
    /// Never reaches a person. A recipe whose photo could not be had is still
    /// the recipe, so this is the reason a picture was skipped rather than a
    /// reason an import failed.
    /// </remarks>
    public static readonly Error NotAPicture = new(
        "import.not_a_picture",
        "That address did not answer with a picture.",
        ErrorType.Validation);

    /// <summary>
    /// A recipe could not be brought over, and the reason is this app's fault.
    /// </summary>
    /// <remarks>
    /// The catch-all an import reports when writing a recipe threw rather than
    /// returned. It is one line in the result and nothing more: a defect in one
    /// recipe must not end the run the other three hundred are part of.
    /// </remarks>
    public static readonly Error CouldNotImport = new(
        "import.could_not_import",
        "That recipe could not be brought over.",
        ErrorType.Failure);

    /// <summary>
    /// No such import to follow: unknown, finished long ago, or somebody
    /// else's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three are one answer on purpose. An import is remembered for a while
    /// after it ends and then forgotten, and a caller learns nothing from being
    /// told which of those happened.
    /// </para>
    /// <para>
    /// The sentence carries no id, unlike its neighbours, because this one is
    /// read by a person: it is what a screen shows when the stream it was
    /// following has gone. It says the two things worth acting on — the recipes
    /// may already be on the shelf, and asking again is safe.
    /// </para>
    /// </remarks>
    public static readonly Error ImportNotFound = new(
        "import.import_not_found",
        "That import is no longer being followed. It may have finished a while ago, or this server may have restarted since — the recipes it had already brought over are on its cookbook, and asking for the rest again is safe.",
        ErrorType.NotFound);

    /// <summary>An import was asked for with nothing in it.</summary>
    public static readonly Error NothingToImport = new(
        "import.nothing_to_import",
        "Choose at least one recipe to bring over.",
        ErrorType.Validation);

    /// <summary>More recipes were asked for in one go than one import may carry.</summary>
    public static readonly Error TooManyAtOnce = new(
        "import.too_many_at_once",
        "That is more recipes than one request brings over. Ask for fewer.",
        ErrorType.Validation);
}
