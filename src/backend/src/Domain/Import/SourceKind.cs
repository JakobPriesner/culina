namespace Domain.Import;

/// <summary>Where an imported recipe came from.</summary>
/// <remarks>A string rather than an enum, as it is stored in the database; <see cref="Parse"/> is the only way in.</remarks>
public sealed record SourceKind
{
    private SourceKind(string code) => Code = code;

    /// <summary>How it is written, on the wire and in the database.</summary>
    public string Code { get; }

    /// <summary>A public web page, read once. No credential and no connection.</summary>
    public static SourceKind Web { get; } = new("web");

    /// <summary>A Tandoor instance, connected with an API token.</summary>
    public static SourceKind Tandoor { get; } = new("tandoor");

    /// <summary>This instance's assistant wrote it. Here because where a recipe came from is one fact with one shape.</summary>
    public static SourceKind Assistant { get; } = new("ai");

    /// <summary>The kinds a household can connect to. <see cref="Web"/> is an origin, not a connectable source.</summary>
    public static IReadOnlyList<SourceKind> Connectable { get; } = [Tandoor];

    /// <summary>Reads a kind, or refuses.</summary>
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
