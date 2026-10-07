using Domain.Shared;

namespace Domain.Import;

/// <summary>
/// Another app's recipe library connected to a household, remembered so repeat imports need no new token.
/// <see cref="Secret"/> goes only to the database and an <c>Authorization</c> header; no response carries it.
/// </summary>
public sealed class RecipeSource
{
    /// <summary>Longer than any name anyone gives a server of their own.</summary>
    public const int MaxLabelLength = 80;

    /// <summary>Longer than any API token, and short enough to refuse a paste of a file.</summary>
    public const int MaxSecretLength = 500;

    private RecipeSource(
        Guid id,
        Guid householdId,
        SourceKind kind,
        string label,
        SourceAddress address,
        string? secret,
        Guid createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset? lastUsedAt,
        long version)
    {
        Id = id;
        HouseholdId = householdId;
        Kind = kind;
        Label = label;
        Address = address;
        Secret = secret;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        LastUsedAt = lastUsedAt;
        Version = version;
    }

    /// <summary>The connection's id.</summary>
    public Guid Id { get; }

    /// <summary>Which household connected it.</summary>
    public Guid HouseholdId { get; }

    /// <summary>Which app it is, and so which strategy reads it.</summary>
    public SourceKind Kind { get; }

    /// <summary>What to call it on screen.</summary>
    public string Label { get; private set; }

    /// <summary>Where it is.</summary>
    public SourceAddress Address { get; }

    /// <summary>
    /// The API token, or null when the stored one can no longer be decrypted (a restore without the key ring).
    /// Never returned to a caller.
    /// </summary>
    public string? Secret { get; }

    /// <summary>Whether this connection has to be made again before it can be read.</summary>
    public bool NeedsReconnecting => Secret is null;

    /// <summary>Who connected it.</summary>
    public Guid CreatedBy { get; }

    /// <summary>When it was connected.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When recipes were last taken from it, or null if never.</summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>Incremented by every write.</summary>
    public long Version { get; private set; }

    /// <summary>Connects a library.</summary>
    /// <param name="householdId">Whose kitchen it belongs to.</param>
    /// <param name="kind">Which app it is.</param>
    /// <param name="label">What to call it.</param>
    /// <param name="address">Where it is.</param>
    /// <param name="secret">The API token.</param>
    /// <param name="createdBy">Who connected it.</param>
    /// <param name="now">The injected current time.</param>
    public static Result<RecipeSource> Create(
        Guid householdId,
        SourceKind kind,
        string? label,
        SourceAddress address,
        string? secret,
        Guid createdBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(address);

        if (!kind.IsConnectable)
        {
            return ImportErrors.UnknownSourceKind;
        }

        var token = secret?.Trim() ?? string.Empty;

        if (token.Length is 0 or > MaxSecretLength)
        {
            return ImportErrors.InvalidSourceToken;
        }

        var name = string.IsNullOrWhiteSpace(label) ? address.Origin.Host : label.Trim();

        return name.Length > MaxLabelLength
            ? ImportErrors.InvalidSourceLabel
            : new RecipeSource(
                CulinaId.New(),
                householdId,
                kind,
                name,
                address,
                token,
                createdBy,
                now,
                lastUsedAt: null,
                version: 1);
    }

    /// <summary>Rebuilds a connection from storage.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="householdId">Whose kitchen it belongs to.</param>
    /// <param name="kind">Which app it is.</param>
    /// <param name="label">What it is called.</param>
    /// <param name="address">Where it is.</param>
    /// <param name="secret">The stored token, or null if it could not be read.</param>
    /// <param name="createdBy">Who connected it.</param>
    /// <param name="createdAt">When it was connected.</param>
    /// <param name="lastUsedAt">When it was last read from.</param>
    /// <param name="version">The stored version.</param>
    public static RecipeSource Restore(
        Guid id,
        Guid householdId,
        SourceKind kind,
        string label,
        SourceAddress address,
        string? secret,
        Guid createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset? lastUsedAt,
        long version) =>
        new(id, householdId, kind, label, address, secret, createdBy, createdAt, lastUsedAt, version);

    /// <summary>Records that recipes were taken from it.</summary>
    /// <param name="now">The injected current time.</param>
    public void Used(DateTimeOffset now)
    {
        LastUsedAt = now;
        Version += 1;
    }
}
