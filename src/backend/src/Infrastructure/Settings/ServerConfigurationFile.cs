using Application.Abstractions.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;

namespace Infrastructure.Settings;

/// <summary>Adds the file an administrator's server settings are saved to, above <c>appsettings</c> and below user secrets, environment and command line; see <see cref="IServerConfiguration"/>.</summary>
public static class ServerConfigurationFile
{
    /// <summary>The file's name inside <c>Storage__ConfigPath</c>.</summary>
    public const string FileName = "culina.json";

    /// <summary>Inserts the file into the configuration's sources.</summary>
    /// <param name="configuration">The host's configuration, before it is built on.</param>
    public static IConfigurationManager AddServerConfigurationFile(this IConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration[SettingsKey.Of(StorageSettings.SectionName, nameof(StorageSettings.ConfigPath))];
        var directory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(configured) ? StorageSettings.DefaultConfigPath : configured);

        configuration.Sources.Insert(AboveAppSettings(configuration.Sources), new ServerConfigurationSource(directory));

        return configuration;
    }

    private static int AboveAppSettings(IList<IConfigurationSource> sources)
    {
        var last = -1;

        for (var index = 0; index < sources.Count; index++)
        {
            if (sources[index] is JsonConfigurationSource { Path: { } path }
                && path.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase))
            {
                last = index;
            }
        }

        return last + 1;
    }
}

/// <summary>The settings file as a configuration source, marked so it can be found among the providers again.</summary>
internal sealed class ServerConfigurationSource : JsonConfigurationSource
{
    /// <summary>Reads <see cref="ServerConfigurationFile.FileName"/> from a directory.</summary>
    /// <param name="directory">The directory, absolute.</param>
    /// <remarks>Read once, never watched: a saved change applies on host restart. A missing directory reads as an empty file.</remarks>
    internal ServerConfigurationSource(string directory)
    {
        Directory = directory;
        Path = ServerConfigurationFile.FileName;
        Optional = true;
        ReloadOnChange = false;
        FileProvider = System.IO.Directory.Exists(directory)
            ? new PhysicalFileProvider(directory)
            : new NullFileProvider();
    }

    /// <summary>The directory the file lives in.</summary>
    internal string Directory { get; }

    /// <summary>The file itself.</summary>
    internal string FilePath => System.IO.Path.Combine(Directory, ServerConfigurationFile.FileName);
}
