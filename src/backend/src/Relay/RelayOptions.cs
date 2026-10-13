namespace Relay;

/// <summary>Everything the relay is told at start: where it keeps its map, how much it lets through, and the store keys.</summary>
/// <remarks>Read once from configuration (section <c>Relay</c>), checked by <see cref="Validate"/> before the host starts.</remarks>
internal sealed record RelayOptions
{
    /// <summary>The SQLite file holding relay token hashes and device tokens.</summary>
    public string DatabasePath { get; init; } = "/data/relay.db";

    /// <summary>How many notifications one relay token, and one source address, may send an hour.</summary>
    public int PerHour { get; init; } = 30;

    /// <summary>Whether the client address comes from <c>X-Forwarded-For</c>, for a relay behind one proxy.</summary>
    public bool BehindProxy { get; init; }

    /// <summary>Apple's push service; null leaves iPhones out.</summary>
    public ApnsOptions? Apns { get; init; }

    /// <summary>Firebase Cloud Messaging; null leaves Android out.</summary>
    public FcmOptions? Fcm { get; init; }

    /// <summary>What is wrong with these options, or nothing.</summary>
    public IReadOnlyList<string> Validate()
    {
        List<string> problems = [];

        if (string.IsNullOrWhiteSpace(DatabasePath))
        {
            problems.Add("Relay:DatabasePath is required.");
        }

        if (PerHour is < 1 or > 10_000)
        {
            problems.Add("Relay:PerHour must be between 1 and 10000.");
        }

        if (Apns is null && Fcm is null)
        {
            problems.Add("Relay needs Relay:Apns, Relay:Fcm or both: with neither it could deliver nothing.");
        }

        problems.AddRange(Apns?.Validate() ?? []);
        problems.AddRange(Fcm?.Validate() ?? []);

        return problems;
    }
}

/// <summary>Token-based APNs: the publisher's <c>.p8</c> key, never a certificate.</summary>
internal sealed record ApnsOptions
{
    /// <summary>The Apple developer team.</summary>
    public string TeamId { get; init; } = string.Empty;

    /// <summary>The key's id, from the developer account.</summary>
    public string KeyId { get; init; } = string.Empty;

    /// <summary>Path to the <c>.p8</c> file.</summary>
    public string PrivateKeyPath { get; init; } = string.Empty;

    /// <summary>The app's bundle id, which APNs calls the topic.</summary>
    public string Topic { get; init; } = string.Empty;

    /// <summary>The sandbox host, for development builds signed with a development profile.</summary>
    public bool Sandbox { get; init; }

    internal IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(TeamId) || string.IsNullOrWhiteSpace(KeyId)
            || string.IsNullOrWhiteSpace(PrivateKeyPath) || string.IsNullOrWhiteSpace(Topic))
        {
            yield return "Relay:Apns needs TeamId, KeyId, PrivateKeyPath and Topic.";
        }
        else if (!File.Exists(PrivateKeyPath))
        {
            yield return "Relay:Apns:PrivateKeyPath names no file.";
        }
    }
}

/// <summary>FCM HTTP v1, authorised as the Firebase project's service account.</summary>
internal sealed record FcmOptions
{
    /// <summary>The Firebase project id.</summary>
    public string ProjectId { get; init; } = string.Empty;

    /// <summary>Path to the service account's JSON key.</summary>
    public string ServiceAccountPath { get; init; } = string.Empty;

    internal IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectId) || string.IsNullOrWhiteSpace(ServiceAccountPath))
        {
            yield return "Relay:Fcm needs ProjectId and ServiceAccountPath.";
        }
        else if (!File.Exists(ServiceAccountPath))
        {
            yield return "Relay:Fcm:ServiceAccountPath names no file.";
        }
    }
}
