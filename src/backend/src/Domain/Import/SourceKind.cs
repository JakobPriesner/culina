namespace Domain.Import;

/// <summary>
/// Where an imported recipe came from.
/// </summary>
/// <remarks>
/// <para>
/// Two shapes of source, not one. <see cref="Web"/> is a page anyone can read
/// without being anyone: paste a link, get one recipe. <see cref="Tandoor"/> is
/// a library that belongs to you, behind a credential, with a thousand recipes
/// in it — connected once and returned to.
/// </para>
/// <para>
/// A string rather than an enum, because it is written into the database and
/// read back out of it, and a stored integer whose meaning lives in a C# file
/// is a column nobody can read. <see cref="Parse"/> is the only way in.
/// </para>
/// </remarks>
public sealed record SourceKind
{
    private SourceKind(string code) => Code = code;

    /// <summary>How it is written, on the wire and in the database.</summary>
    public string Code { get; }

    /// <summary>A public web page, read once. No credential and no connection.</summary>
    public static SourceKind Web { get; } = new("web");

    /// <summary>A Tandoor instance, connected with an API token.</summary>
    public static SourceKind Tandoor { get; } = new("tandoor");

    /// <summary>
    /// This instance's assistant wrote it.
    /// </summary>
    /// <remarks>
    /// Not somewhere else at all, unlike its two neighbours, and here anyway:
    /// where a recipe came from is one fact with one shape, and a second table
    /// for a second kind of elsewhere would be two places to ask one question.
    /// A drafted recipe is an ordinary recipe in every other respect — edited,
    /// cooked, scaled and planned like one somebody typed — and the line under
    /// it saying where it started is the only thing that says otherwise.
    /// </remarks>
    public static SourceKind Assistant { get; } = new("ai");

    /// <summary>
    /// The kinds a household can connect to.
    /// </summary>
    /// <remarks>
    /// <see cref="Web"/> is not among them: there is nothing to connect. It is
    /// a kind an origin can have, not a kind a source can be — which is why the
    /// <c>recipe_sources</c> and <c>recipe_origins</c> tables check different
    /// lists.
    /// </remarks>
    public static IReadOnlyList<SourceKind> Connectable { get; } = [Tandoor];

    /// <summary>Reads a kind, or refuses.</summary>
    /// <param name="code">What arrived on the wire or came out of a row.</param>
    public static SourceKind? Parse(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "web" => Web,
        "tandoor" => Tandoor,
        "ai" => Assistant,
        _ => null
    };

    /// <summary>Whether a household can connect to this kind.</summary>
    public bool IsConnectable => Connectable.Contains(this);

    /// <inheritdoc />
    public override string ToString() => Code;
}
