using Application.Abstractions.Settings;

namespace Application.UnitTests.Abstractions.Settings;

/// <summary>A misconfigured process must fail to start loudly instead of failing confusingly on first use.</summary>
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
        var settings = ValidDatabase() with { Host = host };

        void Act() => settings.Validate();

        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("Database__Host", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void DatabaseValidate_ShouldThrow_WhenThePortIsNotAPortNumber(int port)
    {
        var settings = ValidDatabase() with { Port = port };

        void Act() => settings.Validate();

        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("Database__Port", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DatabaseValidate_ShouldPass_WhenEveryPartIsPresent()
    {
        var settings = ValidDatabase();

        settings.Validate();

        Assert.True(settings.RequireSsl);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(19455)]
    public void PasswordHashingValidate_ShouldThrow_WhenMemoryIsBelowTheOwaspMinimum(int memoryKib)
    {
        var settings = new PasswordHashingSettings { MemoryKib = memoryKib };

        void Act() => settings.Validate();

        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("PasswordHashing__MemoryKib", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordHashingValidate_ShouldPass_WhenTheDefaultsAreUsed()
    {
        var settings = new PasswordHashingSettings();

        settings.Validate();

        Assert.Equal(65536, settings.MemoryKib);
    }

    [Fact]
    public void ForwardedHeadersValidate_ShouldThrow_WhenAProxyIsNotAnIpAddress()
    {
        var settings = new ForwardedHeadersSettings { KnownProxies = ["10.0.0.1", "not-an-ip"] };

        void Act() => settings.Validate();

        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("not-an-ip", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ForwardedHeadersValidate_ShouldAcceptANetworkInCidrForm()
    {
        // A proxy in a container network has no knowable address, so networks must be nameable.
        var settings = new ForwardedHeadersSettings { KnownNetworks = ["172.18.0.0/16", "fd12:3456:789a::/48"] };

        settings.Validate();

        Assert.Equal(2, settings.KnownNetworks.Count);
    }

    [Fact]
    public void ForwardedHeadersValidate_ShouldThrow_WhenANetworkIsNotCidr()
    {
        var settings = new ForwardedHeadersSettings { KnownNetworks = ["172.18.0.1"] };

        void Act() => settings.Validate();

        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("CIDR", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ForwardedHeadersValidate_ShouldSayWhereACidrRangeBelongs()
    {
        // The kind mistake to catch: a CIDR range typed into the addresses list.
        var settings = new ForwardedHeadersSettings { KnownProxies = ["10.0.0.0/8"] };

        void Act() => settings.Validate();

        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("KnownNetworks", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("10.0.0.0/7")]
    [InlineData("::/0")]
    [InlineData("fd00::/8")]
    [InlineData("2000::/3")]
    public void ForwardedHeadersValidate_ShouldThrow_WhenANetworkIsTooWideToTrust(string network)
    {
        // A trusted network lets any client claim any address, which would defeat per-address limits and the security log.
        var settings = new ForwardedHeadersSettings { KnownNetworks = ["172.18.0.0/16", network] };

        void Act() => settings.Validate();

        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains(network, exception.Message, StringComparison.Ordinal);
        Assert.Contains("ForwardedHeaders__DangerouslyTrustWideNetworks", exception.Message, StringComparison.Ordinal);
        Assert.Equal(network, settings.TooWideNetwork());
    }

    [Theory]
    [InlineData("172.16.0.0/12")]
    [InlineData("10.0.0.0/8")]
    [InlineData("2001:db8::/32")]
    [InlineData("fd12:3456:789a::/48")]
    public void ForwardedHeadersValidate_ShouldAcceptANetwork_AtTheWidestItMayBe(string network)
    {
        var settings = new ForwardedHeadersSettings { KnownNetworks = [network] };

        settings.Validate();

        Assert.Null(settings.TooWideNetwork());
    }

    [Fact]
    public void ForwardedHeadersValidate_ShouldAcceptAnyNetwork_WhenTrustingAWideOneIsAskedForByName()
    {
        var settings = new ForwardedHeadersSettings
        {
            KnownNetworks = ["0.0.0.0/0", "::/0"],
            DangerouslyTrustWideNetworks = true
        };

        settings.Validate();

        Assert.Null(settings.TooWideNetwork());
    }

    [Fact]
    public void ForwardedHeadersValidate_ShouldPass_WhenThereAreNoProxies()
    {
        var settings = new ForwardedHeadersSettings();

        settings.Validate();

        Assert.Empty(settings.KnownProxies);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(400)]
    public void CookieValidate_ShouldThrow_WhenTheSessionLifetimeIsUnreasonable(int days)
    {
        var settings = new CookieSettings { SessionDays = days };

        void Act() => settings.Validate();

        Assert.Throws<InvalidOperationException>(Act);
    }

    [Fact]
    public void CookieValidate_ShouldThrow_WhenCookiesAreInsecureWhereNobodyAllowedIt()
    {
        var settings = new CookieSettings { Secure = false };

        void Act() => settings.Validate();

        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("Cookies__AllowInsecureOutsideDevelopment", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CookieValidate_ShouldPass_WhenCookiesAreInsecureWhereTheDeploymentAllowsIt()
    {
        var settings = new CookieSettings { Secure = false, InsecureAllowed = true };

        settings.Validate();

        Assert.False(settings.InsecureWithoutConsent);
    }

    [Theory]
    [InlineData("http://example.com/report")]
    [InlineData("ftp://example.com/report")]
    public void SiteValidate_ShouldThrow_WhenTheSecurityContactIsNotOneTheRfcAllows(string contact)
    {
        var settings = new SiteSettings { SecurityContact = new Uri(contact) };

        void Act() => settings.Validate();

        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("Site__SecurityContact", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("mailto:security@example.com")]
    [InlineData("https://example.com/report")]
    [InlineData("tel:+49-30-1234567")]
    public void SiteValidate_ShouldPass_WhenTheSecurityContactIsOneTheRfcAllows(string contact)
    {
        var settings = new SiteSettings { Url = new Uri("https://culina.example.com"), SecurityContact = new Uri(contact) };

        settings.Validate();

        Assert.NotNull(settings.SecurityContact);
    }

    [Fact]
    public void SiteValidate_ShouldThrow_WhenTheUrlIsNotAWebAddress()
    {
        var settings = new SiteSettings { Url = new Uri("ftp://culina.example.com") };

        void Act() => settings.Validate();

        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("Site__Url", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CookieValidate_ShouldThrow_WhenTheCeilingIsShorterThanTheIdleLifetime()
    {
        var settings = new CookieSettings { SessionDays = 60, MaxSessionDays = 30 };

        void Act() => settings.Validate();

        // The ceiling would silently cut sessions short of the configured lifetime.
        var refused = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("MaxSessionDays", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StorageValidate_ShouldCreateTheDirectories_WhenTheyDoNotExist()
    {
        var root = Path.Combine(Path.GetTempPath(), $"culina-settings-{Guid.CreateVersion7()}");
        var settings = new StorageSettings
        {
            ImagePath = Path.Combine(root, "images"),
            DataProtectionKeyPath = Path.Combine(root, "keys")
        };

        try
        {
            settings.Validate();

            Assert.True(Directory.Exists(settings.ImagePath));
            Assert.True(Directory.Exists(settings.DataProtectionKeyPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
