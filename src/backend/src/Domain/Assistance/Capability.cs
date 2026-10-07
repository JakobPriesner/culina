namespace Domain.Assistance;

/// <summary>
/// One of the four things the assistant can be asked to do. A string like <see cref="AssistantKind"/>, kept
/// per use (not merged into "compose") so the ledger shows where the money went.
/// </summary>
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
