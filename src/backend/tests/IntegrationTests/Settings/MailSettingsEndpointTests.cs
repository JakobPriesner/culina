using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Settings;

/// <summary>Giving Culina a mail account over the wire, and the one value that must never come back over it.</summary>
[Collection(RequiresDatabase.Name)]
public class MailSettingsEndpointTests(PostgresFixture postgres)
{
    private const string Secret = "mail-secret";

    [Fact]
    public async Task Read_ShouldShowMailOff_OnAFreshInstall()
    {
        using var admin = await SharedAdminAsync();

        var read = await admin.GetAsync("/api/v1/settings/mail", Token);

        // Off by default: an instance with no mail account behaves as it did before mail existed.
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.False(read.Json!.Value.GetProperty("enabled").GetBoolean());
        Assert.False(read.Json!.Value.GetProperty("sending").GetBoolean());
    }

    [Fact]
    public async Task ThePassword_ShouldNeverAppearInAnyResponse_OnlyThatThereIsOne()
    {
        using var admin = await postgres.Mail.AdminWithMailAsync(Token);

        var written = await admin.PutAsync("/api/v1/settings/mail", MailHost.Account(), Token);
        var read = await admin.GetAsync("/api/v1/settings/mail", Token);

        Assert.DoesNotContain(Secret, written.Body ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, read.Body ?? string.Empty, StringComparison.Ordinal);
        Assert.True(read.Json!.Value.GetProperty("passwordConfigured").GetBoolean());
        Assert.True(read.Json!.Value.GetProperty("sending").GetBoolean());
        Assert.Equal("https://culina.test/", read.Json!.Value.GetProperty("siteUrl").GetString());
    }

    [Fact]
    public async Task Update_ShouldKeepThePassword_WhenTheFieldIsOmitted()
    {
        using var admin = await postgres.Mail.AdminWithMailAsync(Token);

        await admin.PutAsync("/api/v1/settings/mail", MailHost.Account(password: null), Token);
        var read = await admin.GetAsync("/api/v1/settings/mail", Token);

        Assert.True(read.Json!.Value.GetProperty("passwordConfigured").GetBoolean());
    }

    [Fact]
    public async Task Update_ShouldAskForThePasswordAgain_WhenTheServerChanges()
    {
        using var admin = await postgres.Mail.AdminWithMailAsync(Token);

        var moved = await admin.PutAsync(
            "/api/v1/settings/mail",
            MailHost.Account(host: "smtp.somewhere-else.example", password: null),
            Token);

        // The stored password would otherwise be sent to a server it was never entered for.
        Assert.Equal(HttpStatusCode.BadRequest, moved.StatusCode);
        Assert.Contains("mail.password_required", moved.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_ShouldRefuseToSwitchOn_WithoutAServer()
    {
        using var admin = await postgres.Mail.AdminWithMailAsync(Token);

        var saved = await admin.PutAsync("/api/v1/settings/mail", MailHost.Account(host: ""), Token);

        Assert.Equal(HttpStatusCode.BadRequest, saved.StatusCode);
        Assert.Contains("mail.incomplete", saved.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_ShouldRefuseToSwitchOn_WhenCulinaDoesNotKnowItsAddress()
    {
        using var admin = await SharedAdminAsync();

        // The shared host has no Site:Url, so a link in a mail would have nowhere to point.
        var saved = await admin.PutAsync("/api/v1/settings/mail", MailHost.Account(), Token);

        Assert.Equal(HttpStatusCode.BadRequest, saved.StatusCode);
        Assert.Contains("mail.site_url_required", saved.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Test_ShouldMailTheAdministrator_Straight_Away()
    {
        using var admin = await postgres.Mail.AdminWithMailAsync(Token);

        var tested = await admin.PostAsync("/api/v1/settings/mail/tests", new { }, Token);

        Assert.Equal(HttpStatusCode.NoContent, tested.StatusCode);
        Assert.Single(postgres.Mail.Mail.Sent, one => one.To == MailHost.AdminEmail && one.Subject.Contains("Test", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Test_ShouldSayWhatTheServerAnswered_WhenItRefuses()
    {
        using var admin = await postgres.Mail.AdminWithMailAsync(Token);
        postgres.Mail.Mail.Refusal = "535 Authentication credentials invalid";

        var tested = await admin.PostAsync("/api/v1/settings/mail/tests", new { }, Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, tested.StatusCode);
        Assert.Contains("535 Authentication credentials invalid", tested.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Settings_ShouldBeForbidden_ForAnAccountThatIsNotTheAdministrator()
    {
        using var admin = await SharedAdminAsync();
        using var other = postgres.Api.NewApiClient();
        await admin.PutAsync("/api/v1/settings/registration", new { openRegistration = true, requireInvitation = false, maxUsers = 10 }, Token);
        await other.PostAsync("/api/v1/users", new { email = "bob@example.com", displayName = "Bob", password = MailHost.Password }, Token);
        await other.PostAsync("/api/v1/sessions", new { email = "bob@example.com", password = MailHost.Password }, Token);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/v1/settings/mail", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsync("/api/v1/settings/mail", MailHost.Account(), Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync("/api/v1/settings/mail/tests", new { }, Token)).StatusCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ApiClient> SharedAdminAsync()
    {
        await postgres.ResetAsync(Token);

        var client = postgres.Api.NewApiClient();

        await client.PostAsync("/api/v1/users", new { email = MailHost.AdminEmail, displayName = "Ada", password = MailHost.Password }, Token);
        await client.PostAsync("/api/v1/sessions", new { email = MailHost.AdminEmail, password = MailHost.Password }, Token);

        return client;
    }
}
