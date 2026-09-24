using Application.Abstractions.Settings;
using Domain.Shared;

namespace TestSupport;

/// <summary>
/// The configuration a process started with, in memory, and a settings file
/// that records what was written to it.
/// </summary>
public sealed class FakeServerConfiguration : IServerConfiguration
{
    private readonly Dictionary<string, string> values;

    /// <param name="values">What the process started with, by <c>Section:Key</c>.</param>
    /// <param name="pinned">The keys the environment sets.</param>
    public FakeServerConfiguration(
        IReadOnlyDictionary<string, string>? values = null,
        IEnumerable<string>? pinned = null)
    {
        this.values = new Dictionary<string, string>(values ?? new Dictionary<string, string>());
        Pins = [.. pinned ?? []];
    }

    /// <summary>The keys the environment sets.</summary>
    public HashSet<string> Pins { get; }

    /// <summary>Whether the settings file can be written.</summary>
    public bool Writable { get; set; } = true;

    /// <summary>What was last written, or null if nothing was.</summary>
    public IReadOnlyDictionary<string, string>? Saved { get; private set; }

    public string? Read(string key) => values.GetValueOrDefault(key);

    public bool IsPinned(string key) => Pins.Contains(key);

    public bool CanSave() => Writable;

    public Task<Result> SaveAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        if (!Writable)
        {
            return Task.FromResult(Result.Failure(SettingsErrors.NotWritable));
        }

        Saved = values.Where(entry => !IsPinned(entry.Key)).ToDictionary();

        return Task.FromResult(Result.Success());
    }
}
