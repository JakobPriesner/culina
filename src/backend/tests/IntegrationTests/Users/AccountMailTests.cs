using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Users;

/// <summary>
/// What Culina mails once the administrator has given it a mail account, and that every link in those mails works
/// once, for the right account, and only for what it was sent for.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class AccountMailTests(PostgresFixture postgres)
{
    private const string Bob = "bob@example.com";
    private const string NewPassword = "a sentence nobody else would pick";
    private const string Firefox = "Mozilla/5.0 (Macintosh; Intel Mac OS X 14.5; rv:131.0) Gecko/20100101 Firefox/131.0";
    private const string ChromeOnWindows = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";

    private MailHost Host => postgres.Mail;

    [Fact]
    public async Task Register_ShouldAskForConfirmation_AndRefuseSignIn_UntilTheLinkIsOpened()
    {
        using var admin = await Host.AdminWithMailAsync(Token);
        await OpenRegistrationAsync(admin);
        using var bob = Host.Api.NewApiClient();

        var registered = await bob.PostAsync("/api/v1/users", NewAccount(Bob), Token);
        var refused = await bob.PostAsync("/api/v1/sessions", Credentials(Bob, MailHost.Password), Token);

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        Assert.True(registered.Json!.Value.GetProperty("emailConfirmationRequired").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("auth.email_unconfirmed", refused.ProblemCode);

        var mail = Assert.Single(await Host.Mail.To(Bob, 1, Token));
        Assert.Contains("https://culina.test/confirm-email#token=", mail.Text, StringComparison.Ordinal);

        var confirmed = await bob.PostAsync("/api/v1/email-confirmations", new { token = MailHost.TokenIn(mail) }, Token);

        // Opening the link signs this browser in, and the password works from then on.
        Assert.Equal(HttpStatusCode.Created, confirmed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync("/api/v1/users/me", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SignInAsync(Bob, MailHost.Password)).StatusCode);
    }

    [Fact]
    public async Task ConfirmationLink_ShouldWorkOnlyOnce()
    {
        using var admin = await Host.AdminWithMailAsync(Token);
        await OpenRegistrationAsync(admin);
        using var bob = Host.Api.NewApiClient();
        await bob.PostAsync("/api/v1/users", NewAccount(Bob), Token);
        var token = MailHost.TokenIn(Assert.Single(await Host.Mail.To(Bob, 1, Token)));

        await bob.PostAsync("/api/v1/email-confirmations", new { token }, Token);
        var again = await Host.Api.NewApiClient().PostAsync("/api/v1/email-confirmations", new { token }, Token);

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("auth.invalid_link", again.ProblemCode);
    }

    [Fact]
    public async Task SignInRefusedForAnUnconfirmedAddress_ShouldSendTheLinkAgain()
    {
        using var admin = await Host.AdminWithMailAsync(Token);
        await OpenRegistrationAsync(admin);
        await Host.Api.NewApiClient().PostAsync("/api/v1/users", NewAccount(Bob), Token);
        await Host.Mail.To(Bob, 1, Token);

        // A minute passes in the throttle's eyes only for a different account, so this proves the resend is the
        // only mail the refusal sends, not that it sends one per attempt.
        await SignInAsync(Bob, MailHost.Password);
        await SignInAsync(Bob, MailHost.Password);

        var mails = await Host.Mail.To(Bob, 2, Token);

        Assert.InRange(mails.Count, 1, 2);
        Assert.All(mails, one => Assert.Contains("confirm-email#token=", one.Text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task WrongPassword_ShouldNotSayTheAddressIsUnconfirmed()
    {
        using var admin = await Host.AdminWithMailAsync(Token);
        await OpenRegistrationAsync(admin);
        await Host.Api.NewApiClient().PostAsync("/api/v1/users", NewAccount(Bob), Token);

        var refused = await SignInAsync(Bob, "not the password at all");

        // Only somebody who knows the password learns anything about the account.
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Equal("auth.invalid_credentials", refused.ProblemCode);
    }

    [Fact]
    public async Task SignInLink_ShouldSignIn_AndWorkOnlyOnce()
    {
        using var admin = await Host.AdminWithMailAsync(Token);
        using var browser = Host.Api.NewApiClient();

        var asked = await browser.PostAsync("/api/v1/sign-in-links", new { email = MailHost.AdminEmail }, Token);
        var mail = (await Host.Mail.To(MailHost.AdminEmail, 1, Token)).Single(one => one.Text.Contains("login/link#token=", StringComparison.Ordinal));
        var token = MailHost.TokenIn(mail);

        var redeemed = await browser.PostAsync("/api/v1/sign-in-links/redemptions", new { token }, Token);
        var again = await Host.Api.NewApiClient().PostAsync("/api/v1/sign-in-links/redemptions", new { token }, Token);

        Assert.Equal(HttpStatusCode.Accepted, asked.StatusCode);
        Assert.Equal(HttpStatusCode.Created, redeemed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/v1/users/me", Token)).StatusCode);
        Assert.Equal("auth.invalid_link", again.ProblemCode);
    }

    [Fact]
    public async Task ConfirmationToken_ShouldNotSignInAsASignInLink()
    {
        using var admin = await Host.AdminWithMailAsync(Token);
        await OpenRegistrationAsync(admin);
        await Host.Api.NewApiClient().PostAsync("/api/v1/users", NewAccount(Bob), Token);
        var token = MailHost.TokenIn(Assert.Single(await Host.Mail.To(Bob, 1, Token)));

        var used = await Host.Api.NewApiClient().PostAsync("/api/v1/sign-in-links/redemptions", new { token }, Token);

        Assert.Equal("auth.invalid_link", used.ProblemCode);
    }

    [Fact]
    public async Task Links_ShouldBeAccepted_ForAnAddressNobodyHas_AndMailNothing()
    {
        using var admin = await Host.AdminWithMailAsync(Token);
        using var stranger = Host.Api.NewApiClient();

        var signIn = await stranger.PostAsync("/api/v1/sign-in-links", new { email = "nobody@example.com" }, Token);
        var reset = await stranger.PostAsync("/api/v1/password-reset-links", new { email = "nobody@example.com" }, Token);

        // The same answer as for a real address, so these cannot be used to find out who has an account.
        Assert.Equal(HttpStatusCode.Accepted, signIn.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, reset.StatusCode);
        Assert.Empty(await Host.Mail.To("nobody@example.com", 1, Token));
    }

    [Fact]
    public async Task PasswordResetLink_ShouldMailACode_ThatSetsANewPassword_AndSayItChanged()
    {
        using var admin = await Host.AdminWithMailAsync(Token);
        using var browser = Host.Api.NewApiClient();

        await browser.PostAsync("/api/v1/password-reset-links", new { email = MailHost.AdminEmail }, Token);
        var mail = (await Host.Mail.To(MailHost.AdminEmail, 1, Token)).Single(one => one.Text.Contains("password-reset#email=", StringComparison.Ordinal));

        var reset = await browser.PostAsync(
            "/api/v1/password-resets",
            new { email = MailHost.AdminEmail, code = MailHost.CodeIn(mail), password = NewPassword },
            Token);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SignInAsync(MailHost.AdminEmail, NewPassword)).StatusCode);

        var mails = await Host.Mail.To(MailHost.AdminEmail, 2, Token);
        Assert.Contains(mails, one => one.Subject == "Your Culina password was changed");
    }

    [Fact]
    public async Task MailedResetCode_ShouldNotCountAsASavedRecoveryCode()
    {
        using var admin = await Host.AdminWithMailAsync(Token);

        await Host.Api.NewApiClient().PostAsync("/api/v1/password-reset-links", new { email = MailHost.AdminEmail }, Token);
        await Host.Mail.To(MailHost.AdminEmail, 1, Token);
        var saved = await admin.GetAsync("/api/v1/users/me/recovery-codes", Token);

        // A code in an inbox that expires within the hour is not one of the ten kept in a drawer.
        Assert.Equal(0, saved.Json!.Value.GetProperty("remaining").GetInt32());
    }

    [Fact]
    public async Task NewDevice_ShouldBeMailed_OnlyTheFirstTimeItSignsIn()
    {
        using var admin = await Host.AdminWithMailAsync(Token);
        await SignInFromAsync(Firefox);
        // Setting up signed in from a client with no user agent, so Firefox was news too; out of the way first.
        await Host.Mail.To(MailHost.AdminEmail, 1, Token);
        Host.Mail.Clear();

        await SignInFromAsync(ChromeOnWindows);
        await SignInFromAsync(ChromeOnWindows);
        await SignInFromAsync(Firefox);

        var mails = await Host.Mail.To(MailHost.AdminEmail, 2, Token);
        var notice = Assert.Single(mails);
        Assert.Equal("New sign-in to your Culina account", notice.Subject);
        Assert.Contains("Chrome · Windows", notice.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecoveryCodes_ShouldBeMailedAbout_WhenANewSetIsMade()
    {
        using var admin = await Host.AdminWithMailAsync(Token);

        await admin.PostAsync("/api/v1/users/me/recovery-codes", new { password = MailHost.Password }, Token);

        var mails = await Host.Mail.To(MailHost.AdminEmail, 1, Token);
        var notice = Assert.Single(mails, one => one.Subject == "New recovery codes for your Culina account");

        // The notice says something happened; the codes themselves are only ever in the one response.
        Assert.DoesNotMatch("[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}", notice.Text);
    }

    [Fact]
    public async Task Invitation_ShouldBeMailed_ToTheAddressGiven()
    {
        using var admin = await Host.AdminWithMailAsync(Token);
        var householdId = (await admin.GetAsync("/api/v1/households", Token)).Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

        var invited = await admin.PostAsync($"/api/v1/households/{householdId}/invitations", new { email = "carol@example.com" }, Token);

        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        Assert.Equal("carol@example.com", invited.Json!.Value.GetProperty("mailedTo").GetString());

        var mail = Assert.Single(await Host.Mail.To("carol@example.com", 1, Token));
        Assert.Contains($"https://culina.test/join/{invited.Json!.Value.GetProperty("code").GetString()}", mail.Text, StringComparison.Ordinal);
        Assert.Contains("Ada", mail.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invitation_ShouldStillBeACodeToShare_WithNoAddress()
    {
        using var admin = await Host.AdminWithMailAsync(Token);
        var householdId = (await admin.GetAsync("/api/v1/households", Token)).Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

        // The body-less POST the app has always sent, as a browser sends it: no type, a length of zero.
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/households/{householdId}/invitations")
        {
            Content = new ByteArrayContent([])
        };
        request.Content.Headers.ContentLength = 0;
        var invited = await admin.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        Assert.Equal(JsonValueKind.Null, invited.Json!.Value.GetProperty("mailedTo").ValueKind);
    }

    [Fact]
    public async Task WithMailOff_ShouldRegisterAndSignIn_AsBefore()
    {
        await postgres.ResetAsync(Token);
        using var admin = postgres.Api.NewApiClient();
        await admin.PostAsync("/api/v1/users", NewAccount(MailHost.AdminEmail), Token);
        await admin.PostAsync("/api/v1/sessions", Credentials(MailHost.AdminEmail, MailHost.Password), Token);
        await OpenRegistrationAsync(admin);
        using var bob = postgres.Api.NewApiClient();

        var registered = await bob.PostAsync("/api/v1/users", NewAccount(Bob), Token);
        var signedIn = await bob.PostAsync("/api/v1/sessions", Credentials(Bob, MailHost.Password), Token);
        var link = await bob.PostAsync("/api/v1/sign-in-links", new { email = Bob }, Token);

        Assert.False(registered.Json!.Value.GetProperty("emailConfirmationRequired").GetBoolean());
        Assert.Equal(HttpStatusCode.Created, signedIn.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, link.StatusCode);
        Assert.Equal("mail.off", link.ProblemCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static object NewAccount(string email) => new { email, displayName = email.Split('@')[0], password = MailHost.Password };

    private static object Credentials(string email, string password) => new { email, password };

    private static Task<ApiResponse> OpenRegistrationAsync(ApiClient admin) =>
        admin.PutAsync("/api/v1/settings/registration", new { openRegistration = true, requireInvitation = false, maxUsers = 10 }, Token);

    private async Task<ApiResponse> SignInAsync(string email, string password)
    {
        using var client = Host.Api.NewApiClient();

        return await client.PostAsync("/api/v1/sessions", Credentials(email, password), Token);
    }

    private async Task SignInFromAsync(string userAgent)
    {
        using var client = Host.Api.NewApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sessions")
        {
            Content = JsonContent.Create(Credentials(MailHost.AdminEmail, MailHost.Password))
        };
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);

        var signedIn = await client.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.Created, signedIn.StatusCode);
    }
}
