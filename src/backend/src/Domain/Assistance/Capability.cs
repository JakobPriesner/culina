namespace Domain.Assistance;

/// <summary>
/// One of the four things the assistant can be asked to do.
/// </summary>
/// <remarks>
/// <para>
/// A string for the same reason <see cref="AssistantKind"/> is one: every use
/// is written to the ledger, and the admin screen that reads it back is a table
/// somebody has to be able to make sense of.
/// </para>
/// <para>
/// Four capabilities, but only two of them are different work — three compose a
/// recipe and one draws a picture. They are kept apart here anyway, because
/// what this type is for is telling somebody where their money went, and
/// "compose" would answer that question with a word nobody used.
/// </para>
/// </remarks>
public sealed record Capability
{
    private Capability(string code) => Code = code;

    /// <summary>How it is written in the ledger.</summary>
    public string Code { get; }

    /// <summary>Rewriting a recipe somebody already has.</summary>
    public static Capability Improve { get; } = new("improve");

    /// <summary>Writing one from an idea.</summary>
    public static Capability Draft { get; } = new("draft");

    /// <summary>Reading one out of a photograph or a block of text.</summary>
    public static Capability Read { get; } = new("read");

    /// <summary>Drawing a picture for one.</summary>
    public static Capability Draw { get; } = new("draw");

    /// <summary>Everything the assistant can be asked for.</summary>
    public static IReadOnlyList<Capability> All { get; } = [Improve, Draft, Read, Draw];

    /// <summary>Reads a capability, or refuses.</summary>
    /// <param name="code">What arrived on the wire or came out of a row.</param>
    public static Capability? Parse(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "improve" => Improve,
        "draft" => Draft,
        "read" => Read,
        "draw" => Draw,
        _ => null
    };

    /// <inheritdoc />
    public override string ToString() => Code;
}
