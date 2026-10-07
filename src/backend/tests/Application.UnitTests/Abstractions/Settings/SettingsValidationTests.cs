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
    public void ForwardedHeadersValidate_ShouldAcceptANetworkInCidrForm()
    {
        // Arrange
        // A reverse proxy in a container network has no address anyone can know
        // in advance, so a deployment that can only name addresses cannot name
        // its own proxy.
        var settings = new ForwardedHeadersSettings { KnownNetworks = ["172.18.0.0/16", "fd00::/8"] };

        // Act
        settings.Validate();

        // Assert
        Assert.Equal(2, settings.KnownNetworks.Count);
    }

    [Fact]
    public void ForwardedHeadersValidate_ShouldThrow_WhenANetworkIsNotCidr()
    {
        // Arrange
        var settings = new ForwardedHeadersSettings { KnownNetworks = ["172.18.0.1"] };

        // Act
        void Act() => settings.Validate();

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("CIDR", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ForwardedHeadersValidate_ShouldSayWhereACidrRangeBelongs()
    {
        // Arrange
        // The mistake worth catching kindly: a CIDR range typed into the
        // addresses list, which is exactly what an operator behind a container
        // network reaches for first.
        var settings = new ForwardedHeadersSettings { KnownProxies = ["10.0.0.0/8"] };

        // Act
        void Act() => settings.Validate();

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("KnownNetworks", exception.Message, StringComparison.Ordinal);
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
    public void CookieValidate_ShouldThrow_WhenCookiesAreInsecureWhereNobodyAllowedIt()
    {
        // Arrange
        var settings = new CookieSettings { Secure = false };

        // Act
        void Act() => settings.Validate();

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("Cookies__AllowInsecureOutsideDevelopment", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CookieValidate_ShouldPass_WhenCookiesAreInsecureWhereTheDeploymentAllowsIt()
    {
        // Arrange
        var settings = new CookieSettings { Secure = false, InsecureAllowed = true };

        // Act
        settings.Validate();

        // Assert
        Assert.False(settings.InsecureWithoutConsent);
    }

    [Theory]
    [InlineData("http://example.com/report")]
    [InlineData("ftp://example.com/report")]
    public void SiteValidate_ShouldThrow_WhenTheSecurityContactIsNotOneTheRfcAllows(string contact)
    {
        // Arrange
        var settings = new SiteSettings { SecurityContact = new Uri(contact) };

        // Act
        void Act() => settings.Validate();

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("Site__SecurityContact", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("mailto:security@example.com")]
    [InlineData("https://example.com/report")]
    [InlineData("tel:+49-30-1234567")]
    public void SiteValidate_ShouldPass_WhenTheSecurityContactIsOneTheRfcAllows(string contact)
    {
        // Arrange
        var settings = new SiteSettings { Url = new Uri("https://culina.example.com"), SecurityContact = new Uri(contact) };

        // Act
        settings.Validate();

        // Assert
        Assert.NotNull(settings.SecurityContact);
    }

    [Fact]
    public void SiteValidate_ShouldThrow_WhenTheUrlIsNotAWebAddress()
    {
        // Arrange
        var settings = new SiteSettings { Url = new Uri("ftp://culina.example.com") };

        // Act
        void Act() => settings.Validate();

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("Site__Url", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CookieValidate_ShouldThrow_WhenTheCeilingIsShorterThanTheIdleLifetime()
    {
        // Arrange
        var settings = new CookieSettings { SessionDays = 60, MaxSessionDays = 30 };

        // Act
        void Act() => settings.Validate();

        // Assert
        // The ceiling would silently cut every session short of the lifetime
        // the setting beside it promises.
        var refused = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("MaxSessionDays", refused.Message, StringComparison.Ordinal);
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
