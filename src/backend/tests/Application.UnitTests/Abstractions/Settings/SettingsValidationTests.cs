using Application.Abstractions.Settings;

namespace Application.UnitTests.Abstractions.Settings;

/// <summary>
/// A misconfigured process must fail to start loudly. These assert that each
/// record rejects the values that would otherwise cause a confusing failure on
/// the first request that needed them.
/// </summary>
public class SettingsValidationTests
{
    private static DatabaseSettings ValidDatabase() => new()
    {
        Host = "localhost",
        Port = 5432,
        Name = "culina",
        Username = "culina_app",
        Password = "secret"
    };

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DatabaseValidate_ShouldThrow_WhenTheHostIsBlank(string host)
    {
        // Arrange
        var settings = ValidDatabase() with { Host = host };

        // Act
        void Act() => settings.Validate();

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("Database__Host", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void DatabaseValidate_ShouldThrow_WhenThePortIsNotAPortNumber(int port)
    {
        // Arrange
        var settings = ValidDatabase() with { Port = port };

        // Act
        void Act() => settings.Validate();

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("Database__Port", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DatabaseValidate_ShouldPass_WhenEveryPartIsPresent()
    {
        // Arrange
        var settings = ValidDatabase();

        // Act
        settings.Validate();

        // Assert
        Assert.True(settings.RequireSsl);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(19455)]
    public void PasswordHashingValidate_ShouldThrow_WhenMemoryIsBelowTheOwaspMinimum(int memoryKib)
    {
        // Arrange
        var settings = new PasswordHashingSettings { MemoryKib = memoryKib };

        // Act
        void Act() => settings.Validate();

        // Assert
        // An operator must not be able to weaken the hash by accident.
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("PasswordHashing__MemoryKib", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordHashingValidate_ShouldPass_WhenTheDefaultsAreUsed()
    {
        // Arrange
        var settings = new PasswordHashingSettings();

        // Act
        settings.Validate();

        // Assert
        Assert.Equal(65536, settings.MemoryKib);
    }

    [Fact]
    public void ForwardedHeadersValidate_ShouldThrow_WhenAProxyIsNotAnIpAddress()
    {
        // Arrange
        var settings = new ForwardedHeadersSettings { KnownProxies = ["10.0.0.1", "not-an-ip"] };

        // Act
        void Act() => settings.Validate();

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("not-an-ip", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ForwardedHeadersValidate_ShouldPass_WhenThereAreNoProxies()
    {
        // Arrange
        var settings = new ForwardedHeadersSettings();

        // Act
        settings.Validate();

        // Assert
        Assert.Empty(settings.KnownProxies);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(400)]
    public void CookieValidate_ShouldThrow_WhenTheSessionLifetimeIsUnreasonable(int days)
    {
        // Arrange
        var settings = new CookieSettings { SessionDays = days };

        // Act
        void Act() => settings.Validate();

        // Assert
        Assert.Throws<InvalidOperationException>(Act);
    }

    [Fact]
    public void StorageValidate_ShouldCreateTheDirectories_WhenTheyDoNotExist()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), $"culina-settings-{Guid.CreateVersion7()}");
        var settings = new StorageSettings
        {
            ImagePath = Path.Combine(root, "images"),
            DataProtectionKeyPath = Path.Combine(root, "keys")
        };

        try
        {
            // Act
            settings.Validate();

            // Assert
            Assert.True(Directory.Exists(settings.ImagePath));
            Assert.True(Directory.Exists(settings.DataProtectionKeyPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
