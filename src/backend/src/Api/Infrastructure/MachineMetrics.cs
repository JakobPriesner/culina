using System.Diagnostics.Metrics;
using Application.Abstractions.Settings;

namespace Api.Infrastructure;

/// <summary>
/// What the machine has left: memory, and room on the volumes Culina writes to.
/// </summary>
/// <remarks>
/// <para>
/// The runtime and process instrumentation say what this process uses; neither
/// says how close the machine is to running out. A self-hosted instance most
/// often dies of a full disk or a container memory limit, and both are cheap
/// to read on every collection.
/// </para>
/// <para>
/// Memory comes from the garbage collector, which already knows the container's
/// limit and the load at its last collection, so no platform-specific reading
/// is needed. Machine-wide CPU, network and disk throughput are left to a
/// collector's host metrics receiver, which reads them properly on every
/// platform.
/// </para>
/// </remarks>
internal sealed class MachineMetrics : IDisposable
{
    internal const string MeterName = "Culina.Machine";

    private readonly Meter meter = new(MeterName);

    /// <param name="storage">
    /// The volumes to watch; absent on the setup host, which writes nowhere.
    /// </param>
    internal MachineMetrics(StorageSettings? storage)
    {
        meter.CreateObservableUpDownCounter(
            "system.memory.limit",
            () => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            unit: "By",
            description: "Memory available to the process: the container's limit, or the machine's.");

        meter.CreateObservableUpDownCounter(
            "system.memory.usage",
            () => new Measurement<long>(
                GC.GetGCMemoryInfo().MemoryLoadBytes,
                new KeyValuePair<string, object?>("system.memory.state", "used")),
            unit: "By",
            description: "Memory in use on the machine or in the container, as of the last garbage collection.");

        if (storage is null)
        {
            return;
        }

        (string Name, string Path)[] volumes =
        [
            ("images", storage.ImagePath),
            ("keys", storage.DataProtectionKeyPath),
            ("config", storage.ConfigPath)
        ];

        meter.CreateObservableUpDownCounter(
            "culina.storage.usage",
            () => volumes.SelectMany(Usage),
            unit: "By",
            description: "Space used and still free on the volume holding each of Culina's directories.");

        meter.CreateObservableUpDownCounter(
            "culina.storage.limit",
            () => volumes.SelectMany(Limit),
            unit: "By",
            description: "Size of the volume holding each of Culina's directories.");
    }

    public void Dispose() => meter.Dispose();

    private static IEnumerable<Measurement<long>> Usage((string Name, string Path) volume)
    {
        if (SpaceOf(volume.Path) is not { } space)
        {
            yield break;
        }

        yield return new Measurement<long>(space.Total - space.Free, Tags(volume.Name, "used"));
        yield return new Measurement<long>(space.Available, Tags(volume.Name, "free"));
    }

    private static IEnumerable<Measurement<long>> Limit((string Name, string Path) volume)
    {
        if (SpaceOf(volume.Path) is { } space)
        {
            yield return new Measurement<long>(space.Total, Tags(volume.Name, null));
        }
    }

    /// <summary>
    /// The space on the volume a directory is on, or nothing when it cannot be
    /// read — the configuration directory is optional, and a collection must
    /// never throw.
    /// </summary>
    private static Space? SpaceOf(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return null;
            }

            var drive = new DriveInfo(path);

            return new Space(drive.TotalSize, drive.TotalFreeSpace, drive.AvailableFreeSpace);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static KeyValuePair<string, object?>[] Tags(string directory, string? state) =>
        state is null
            ? [new("culina.storage.directory", directory)]
            : [new("culina.storage.directory", directory), new("system.filesystem.state", state)];

    /// <param name="Total">The size of the volume.</param>
    /// <param name="Free">What is free, including what only a superuser may use.</param>
    /// <param name="Available">What is free for this process.</param>
    private readonly record struct Space(long Total, long Free, long Available);
}
