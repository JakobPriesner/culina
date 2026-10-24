using System.Net;
using System.Text.RegularExpressions;
using Application.Abstractions;
using Infrastructure.Mail;
using Microsoft.Extensions.DependencyInjection;
using TestSupport;

namespace IntegrationTests.Fixtures;

/// <summary>
/// A host that knows its public address and sends mail into a <see cref="RecordingMailTransport"/>; its own host, so
/// the shared one keeps behaving like an instance with no mail.
/// </summary>
public sealed partial class MailHost : IDisposable
{
    public const string Password = "correct horse battery staple";

    public const string AdminEmail = "ada@example.com";

    private readonly PostgresFixture postgres;

    public MailHost(PostgresFixture postgres)
    {
        this.postgres = postgres;

        // Shared by every host in the run; the others have mail off and do not look.
        MailOutboxWorker.Poll = TimeSpan.FromMilliseconds(50);

        Api = new CulinaApiFactory(
            postgres,
            overrides: new Dictionary<string, string> { ["Site:Url"] = "https://culina.test" },
            replace: services => services.AddSingleton<IMailTransport>(Mail));
    }

    public RecordingMailTransport Mail { get; } = new();

    public CulinaApiFactory Api { get; }

    /// <summary>An empty instance whose first account (the administrator) has switched mail on.</summary>
    public async Task<ApiClient> AdminWithMailAsync(CancellationToken cancellationToken)
    {
        await postgres.ResetAsync(cancellationToken);
        Mail.Clear();
        Mail.Refusal = null;

        var admin = Api.NewApiClient();

        await admin.PostAsync("/api/v1/users", new { email = AdminEmail, displayName = "Ada", password = Password }, cancellationToken);
        await admin.PostAsync("/api/v1/sessions", new { email = AdminEmail, password = Password }, cancellationToken);

        var saved = await admin.PutAsync("/api/v1/settings/mail", Account(), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        return admin;
    }

    /// <summary>A complete mail account, switched on.</summary>
    public static object Account(bool enabled = true, string host = "smtp.example.com", string? password = "mail-secret") => new
    {
        enabled,
        host,
        port = 587,
        security = "starttls",
        username = "culina@example.com",
        password,
        fromAddress = "culina@example.com",
        fromName = "Culina"
    };

    /// <summary>What follows <c>#token=</c> in a mailed link.</summary>
    public static string TokenIn(OutgoingMail mail) => TokenPattern().Match(mail.Text).Groups[1].Value;

    /// <summary>The recovery code a password-forgotten mail carries.</summary>
    public static string CodeIn(OutgoingMail mail) => CodePattern().Match(mail.Text).Groups[1].Value;

    public void Dispose() => Api.Dispose();

    [GeneratedRegex("#token=([A-Za-z0-9_-]+)")]
    private static partial Regex TokenPattern();

    [GeneratedRegex("code=([0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4})")]
    private static partial Regex CodePattern();
}
