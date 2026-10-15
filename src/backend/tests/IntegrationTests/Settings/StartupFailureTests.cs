using System.Reflection;
using Api.Infrastructure;

namespace IntegrationTests.Settings;

/// <summary>
/// A host that cannot start must exit non-zero rather than throw out of Main: in the chiseled image an
/// unhandled exception printed its stack trace and then left the process at 99% CPU with the container
/// still up, so nothing restarted it.
/// </summary>
public class StartupFailureTests
{
    [Fact]
    public void Main_ShouldExitNonZero_WhenTheSettingsAreInvalid()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string[] args =
        [
            "--environment=Production",
            "--urls=http://127.0.0.1:0",
            "--Database:Host=127.0.0.1",
            "--Database:Name=culina",
            "--Database:Username=culina",
            "--Database:Password=unused",
            $"--Storage:ImagePath={root}/images",
            $"--Storage:DataProtectionKeyPath={root}/keys",
            $"--Storage:ConfigPath={root}/config",

            // Refused outside Development without an explicit opt-in.
            "--Cookies:Secure=false",
        ];

        try
        {
            var exitCode = typeof(HostRestart).Assembly.EntryPoint!.Invoke(null, [args]);

            Assert.Equal(1, exitCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
