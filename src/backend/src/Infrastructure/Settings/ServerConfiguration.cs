using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Abstractions.Settings;
using Domain.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Infrastructure.Settings;

/// <summary>
/// Reads the configuration this process started with, and writes the settings
/// file an administrator edits.
/// </summary>
/// <param name="configuration">The host's configuration, with the settings file among its sources.</param>
internal sealed class ServerConfiguration(IConfiguration configuration) : IServerConfiguration
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private readonly IConfigurationRoot root = (IConfigurationRoot)configuration;

    public string? Read(string key) => root[key];

    public bool IsPinned(string key)
    {
        var providers = root.Providers.ToList();
        var file = providers.FindIndex(provider => provider is JsonConfigurationProvider { Source: ServerConfigurationSource });

        // An empty value is not a pin. Compose writes `KEY: ${KEY:-}` as an
        // empty variable when the operator set nothing, and the settings
        // extensions already read empty as absent.
        return providers
            .Skip(file + 1)
            .Any(provider => provider.TryGet(key, out var value) && !string.IsNullOrEmpty(value));
    }

    public bool CanSave()
    {
        try
        {
            Directory.CreateDirectory(Source.Directory);

            var probe = Path.Combine(Source.Directory, $".write-probe-{Guid.CreateVersion7():n}");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);

            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task<Result> SaveAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);

        try
        {
            Directory.CreateDirectory(Source.Directory);

            if (await ReadAsync(cancellationToken).ConfigureAwait(false) is not { } document)
            {
                return SettingsErrors.FileUnreadable;
            }

            foreach (var (key, value) in values.Where(entry => !IsPinned(entry.Key)))
            {
                Set(document, key, value);
            }

            await WriteAsync(document, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // The expected failure of a deployment that mounted nothing here —
            // a read-only root filesystem, a directory owned by root. Nothing
            // about the request is wrong, and the screen says what to mount.
            return SettingsErrors.NotWritable;
        }
    }

    private ServerConfigurationSource Source =>
        root.Providers
            .OfType<JsonConfigurationProvider>()
            .Select(provider => provider.Source)
            .OfType<ServerConfigurationSource>()
            .SingleOrDefault()
        ?? throw new InvalidOperationException(
            "The settings file is not among the configuration sources. Program must call AddServerConfigurationFile.");

    /// <summary>
    /// The file as it is now, or null when it is no longer a JSON object.
    /// </summary>
    /// <remarks>
    /// The process started from this file, so it parsed then; a file that no
    /// longer parses, or holds something other than an object, was edited by
    /// hand since. Saving over it would silently throw that edit away, so the
    /// caller refuses instead and leaves the file as it is.
    /// </remarks>
    private async Task<JsonObject?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(Source.FilePath))
        {
            return [];
        }

        var stream = File.OpenRead(Source.FilePath);

        await using (stream.ConfigureAwait(false))
        {
            try
            {
                return await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
                    as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Written beside the file and moved over it, so a crash mid-write leaves
    /// the old settings rather than half of the new ones — a truncated file is
    /// one the next startup cannot parse.
    /// </summary>
    private async Task WriteAsync(JsonObject document, CancellationToken cancellationToken)
    {
        var temporary = $"{Source.FilePath}.saving";

        var writer = new StreamWriter(CreatePrivate(temporary));

        await using (writer.ConfigureAwait(false))
        {
            await writer.WriteAsync(document.ToJsonString(Indented).AsMemory(), cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(temporary, Source.FilePath, overwrite: true);
    }

    /// <summary>
    /// Creates a file only the account the app runs as may read, from its
    /// first byte.
    /// </summary>
    /// <remarks>
    /// It is about to hold the database password. Created with the process's
    /// umask and narrowed afterwards, it would be readable by everyone for as
    /// long as the write took. A file left behind by a crash is removed first,
    /// because opening an existing file keeps whatever mode it already had.
    /// </remarks>
    internal static FileStream CreatePrivate(string path)
    {
        File.Delete(path);

        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return new FileStream(path, options);
    }

    private static void Set(JsonObject document, string key, string value)
    {
        var separator = key.IndexOf(':', StringComparison.Ordinal);

        if (separator < 0)
        {
            document[key] = value;

            return;
        }

        var sectionName = key[..separator];

        if (document[sectionName] is not JsonObject section)
        {
            section = [];
            document[sectionName] = section;
        }

        section[key[(separator + 1)..]] = value;
    }
}
