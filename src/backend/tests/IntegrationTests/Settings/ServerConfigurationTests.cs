using System.Runtime.Versioning;
using Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using TestSupport;

namespace IntegrationTests.Settings;

/// <summary>The settings file holds the database password, so only the app's account may read it, even mid-write.</summary>
[UnsupportedOSPlatform("windows")]
public sealed class ServerConfigurationTests : IDisposable
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly string directory = Directory.CreateTempSubdirectory("culina-config").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string SettingsFile => Path.Combine(directory, ServerConfigurationFile.FileName);

    [Fact]
    public async Task CreatePrivate_ShouldCreateAFileOnlyItsOwnerMayRead_BeforeAnythingIsWrittenToIt()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes.");
        var path = $"{SettingsFile}.saving";

        var stream = ServerConfiguration.CreatePrivate(path);

        await using (stream)
        {
            Assert.Equal(0, stream.Length);
            Assert.Equal(OwnerOnly, File.GetUnixFileMode(path));
        }
    }

    [Fact]
    public async Task Save_ShouldLeaveAFileOnlyItsOwnerMayRead_WhenACrashLeftAReadableTemporaryFile()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes.");
        var configuration = Configuration();
        var leftover = $"{SettingsFile}.saving";
        await File.WriteAllTextAsync(leftover, "{}", Token);
        File.SetUnixFileMode(leftover, OwnerOnly | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        var result = await configuration.SaveAsync(
            new Dictionary<string, string> { ["Database:Password"] = "secret" },
            Token);

        result.ShouldBeSuccess();
        Assert.Equal(OwnerOnly, File.GetUnixFileMode(SettingsFile));
        Assert.False(File.Exists(leftover));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private ServerConfiguration Configuration()
    {
        var manager = new ConfigurationManager();
        manager["Storage:ConfigPath"] = directory;
        manager.AddServerConfigurationFile();

        return new ServerConfiguration(manager);
    }
}
